using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CMS.API.Models;
using CMS.API.Security;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CMS.API.Tests;

/// <summary>
/// The pipeline-wide authorization rule: <c>Program.cs</c> applies
/// <c>MapControllers().RequireAuthorization()</c>, and <c>AuthController</c> is the single
/// <c>[AllowAnonymous]</c> opt-out. These tests exercise the real bearer middleware — the key
/// is read from the (faked) <c>SysConfig</c> row exactly as it is in production.
/// </summary>
public class AuthorizationTests
{
    // ---------- A protected endpoint needs a token ----------

    [Fact]
    public async Task ProtectedEndpoint_WithoutAnyToken_Returns401()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAValidToken_Returns200AndTheRows()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = await response.Content.ReadFromJsonAsync<List<AppRole>>();
        Assert.Equal(2, roles!.Count);
    }

    /// <summary>The 401 carries the bearer challenge, so a client can tell auth from a 403 or a 404.</summary>
    [Fact]
    public async Task ProtectedEndpoint_WithoutAnyToken_ChallengesWithBearer()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/app-roles");

        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    /// <summary>
    /// The rule is applied to the whole controller surface at once, not decorated class by class,
    /// so every feature — including the write verbs and the lookups the Angular forms call — is
    /// covered without anyone remembering to add an attribute.
    /// </summary>
    [Theory]
    [InlineData("/api/app-roles")]
    [InlineData("/api/app-users")]
    [InlineData("/api/publish-statuses")]
    [InlineData("/api/courses")]
    [InlineData("/api/partners")]
    [InlineData("/api/course-groups")]
    [InlineData("/api/lookups/app-roles")]
    public async Task EveryFeatureEndpoint_WithoutAToken_Returns401(string url)
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedWriteEndpoint_WithoutAToken_Returns401AndWritesNothing()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            RoleId = "Editor",
            RoleName = "Editor",
            PermissionLevel = 50,
            UserIds = []
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, (await factory.Repository.GetAllAsync()).Count());
    }

    // ---------- Malformed, mis-signed and expired tokens ----------

    [Fact]
    public async Task ProtectedEndpoint_WithAGarbageToken_Returns401()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A well-formed token signed with any other key is rejected — the signature is checked.</summary>
    [Fact]
    public async Task ProtectedEndpoint_WithATokenSignedByAnotherKey_Returns401()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokens.Issue(signingKey: "a-completely-different-signing-key!!"));

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Past the 24-hour lifetime the same token stops working.</summary>
    [Fact]
    public async Task ProtectedEndpoint_WithAnExpiredToken_Returns401()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokens.Issue(issuedAt: DateTimeOffset.UtcNow - JwtTokenService.TokenLifetime - TimeSpan.FromMinutes(1)));

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Rotating the <c>SysConfig</c> key invalidates tokens signed with the old one on the very
    /// next request — the middleware re-reads the row rather than caching it at startup.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_AfterTheSigningKeyIsRotated_RejectsTheOldToken()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/app-roles")).StatusCode);

        factory.SysConfig.Config = new AppConfig
        {
            SymmetricSecurityKey = "rotated-signing-key-0123456789abc"
        };

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/app-roles")).StatusCode);
    }

    // ---------- The Admin role gate on 系統管理 ----------

    /// <summary>
    /// A token with no role claims authenticates but does not get into 系統管理 — <c>403</c>,
    /// not <c>401</c>: we know who the caller is, they may simply not do this.
    /// <para>
    /// This assertion used to read <c>200</c>. Authentication was the whole of the policy then,
    /// and the Admin gate lived only in the Angular sidebar; <c>spec/auth/Authorization.md</c>
    /// recorded that as deliberate scope. The security audit's Finding 1 reversed it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_WithARolelessToken_Returns403()
    {
        using var factory = new AppRoleApiFactory();
        factory.TokenUserId = "Jenny_Tsao";
        factory.TokenUserName = "Jenny Tsao";
        factory.TokenRoles = [];
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Holding some other role is not holding Admin.</summary>
    [Fact]
    public async Task AdminEndpoint_WithAUserRoleToken_Returns403()
    {
        using var factory = new AppRoleApiFactory();
        factory.TokenUserId = "Jenny_Tsao";
        factory.TokenRoles = ["User"];
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/app-roles")).StatusCode);
    }

    /// <summary>
    /// Every 系統管理 endpoint is gated, not just the one the sidebar links to first.
    /// <para>
    /// <c>/api/lookups/*</c> is deliberately split: the two lists that feed the Admin-only forms
    /// are gated on the action, while the rest stay open because the course maintenance forms —
    /// which any signed-in user may use — depend on them. The open ones are asserted below.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("/api/app-users")]
    [InlineData("/api/app-roles")]
    [InlineData("/api/publish-statuses")]
    [InlineData("/api/lookups/app-users")]
    [InlineData("/api/lookups/app-roles")]
    public async Task EveryAdminEndpoint_WithoutTheAdminRole_Returns403(string url)
    {
        using var factory = new LookupApiFactory();
        factory.TokenRoles = ["User"];
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
    }

    /// <summary>
    /// The content features stay open to any signed-in user — that is their daily work, and
    /// over-gating would be as wrong as under-gating.
    /// </summary>
    [Theory]
    [InlineData("/api/lookups/publish-statuses")]
    [InlineData("/api/lookups/course-groups")]
    [InlineData("/api/lookups/partners")]
    public async Task ContentLookup_WithoutTheAdminRole_StillReturns200(string url)
    {
        using var factory = new LookupApiFactory();
        factory.TokenRoles = ["User"];
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
    }

    /// <summary>
    /// The write verbs are gated too, and the 403 lands before the repository is touched.
    /// <para>
    /// <c>PUT /api/app-roles</c> is the one that matters most: <c>AppRoleRequest.UserIds</c>
    /// rewrites the whole <c>AppUserRole</c> membership for a role, so an ungated one would let
    /// any signed-in caller grant themselves Admin in a single request and walk through every
    /// other gate in this file.
    /// </summary>
    [Fact]
    public async Task AdminWriteEndpoint_WithoutTheAdminRole_Returns403AndWritesNothing()
    {
        using var factory = new AppRoleApiFactory();
        factory.TokenRoles = ["User"];
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/app-roles", new AppRoleRequest
        {
            Pkid = 1,
            RoleId = "Admin",
            RoleName = "Administrator",
            PermissionLevel = 1,
            UserIds = ["Jenny_Tsao"]
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // The request asked to replace Admin's three members with just Jenny_Tsao. All three are
        // still there, so SyncUserRolesAsync never ran.
        Assert.Equal(3, (await factory.Repository.GetByIdAsync(1))!.UserIds.Count);
    }

    /// <summary>An Admin token reaches the same endpoint — the gate lets the right people through.</summary>
    [Fact]
    public async Task AdminEndpoint_WithTheAdminRole_Returns200()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/app-roles")).StatusCode);
    }

    // ---------- The account behind the token is re-checked on every request ----------

    /// <summary>
    /// Deactivating an account takes effect on its next request, not whenever its 24-hour token
    /// happens to expire. Without this the 啟用 checkbox would be advisory for up to a day.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_AfterTheAccountIsDeactivated_Returns401()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/app-roles")).StatusCode);

        factory.Accounts.SetActive(TestTokens.DefaultUserId, false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/app-roles")).StatusCode);
    }

    /// <summary>A deleted account is the same story — the token outlives the row, the access does not.</summary>
    [Fact]
    public async Task ProtectedEndpoint_AfterTheAccountIsDeleted_Returns401()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/app-roles")).StatusCode);

        factory.Accounts.Remove(TestTokens.DefaultUserId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/app-roles")).StatusCode);
    }

    /// <summary>
    /// The check runs on every request, not once per token — otherwise a long-lived client would
    /// keep working after the row changed underneath it.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_ChecksTheAccountOnEveryRequest()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        await client.GetAsync("/api/app-roles");
        await client.GetAsync("/api/app-roles");
        await client.GetAsync("/api/app-roles");

        Assert.Equal(3, factory.Accounts.AccountCheckCount);
    }

    /// <summary>
    /// A token whose <c>userId</c> claim names an account that never existed is rejected, so a
    /// token minted against a leaked signing key still buys nothing without a live account.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_WithATokenForAnUnknownAccount_Returns401()
    {
        using var factory = new AppRoleApiFactory { TokenUserId = "nobody@uuu.com.tw" };
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/app-roles")).StatusCode);
    }

    /// <summary>An inactive account cannot ride in on a token either — same answer as the login path.</summary>
    [Fact]
    public async Task ProtectedEndpoint_WithATokenForAnInactiveAccount_Returns401()
    {
        using var factory = new AppRoleApiFactory { TokenUserId = "helen", TokenRoles = ["User"] };
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/app-roles")).StatusCode);
    }

    // ---------- AuthController stays anonymous ----------

    [Fact]
    public async Task Login_WithoutAnyToken_IsReachableAndReturnsAToken()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserId = "miles@uuu.com.tw",
            Password = AuthApiFactory.Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }

    /// <summary>
    /// An anonymous caller with bad credentials gets the credential 401 from the controller, not
    /// the middleware's challenge — proof the request reached <c>AuthController</c> rather than
    /// being turned away by <c>RequireAuthorization</c>.
    /// </summary>
    [Fact]
    public async Task Login_WithBadCredentials_Returns401FromTheControllerNotTheMiddleware()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserId = "miles@uuu.com.tw",
            Password = "not-the-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(response.Headers.WwwAuthenticate);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("登入失敗", problem!.Title);
    }

    /// <summary>Validation still runs for the anonymous endpoint — 400, not 401.</summary>
    [Fact]
    public async Task Login_WithABlankUserId_Returns400ForAnAnonymousCaller()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserId = string.Empty,
            Password = AuthApiFactory.Password
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Gaps found by the ship-workflow coverage audit ----------

    /// <summary>
    /// <c>POST /api/app-users/{id}/reset-password</c> is the endpoint the Admin gate exists for:
    /// it rewrites someone else's <c>PasswordHash</c> to the <c>SysConfig</c> default, which every
    /// account is opened with and every operator therefore knows. Ungated, any signed-in caller
    /// could reset an Admin's password and then simply log in as them.
    /// </summary>
    [Fact]
    public async Task ResetPassword_WithoutTheAdminRole_Returns403AndLeavesThePasswordAlone()
    {
        using var factory = new AppUserApiFactory();
        factory.TokenUserId = "Jenny_Tsao";
        factory.TokenRoles = ["User"];
        using var client = factory.CreateClient();

        var before = factory.Repository.PasswordHashOf(1);

        var response = await client.PostAsync("/api/app-users/1/reset-password", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, factory.Repository.PasswordHashOf(1));
    }

    /// <summary>
    /// The DELETE verb on a 系統管理 controller is gated too, and the 403 lands before the row is
    /// touched — the gate is on the class, so it cannot be verb-specific.
    /// </summary>
    [Fact]
    public async Task AdminDeleteEndpoint_WithoutTheAdminRole_Returns403AndDeletesNothing()
    {
        using var factory = new PublishStatusApiFactory();
        factory.TokenRoles = ["User"];
        using var client = factory.CreateClient();

        var before = (await factory.Repository.GetAllAsync()).Count();

        var response = await client.DeleteAsync("/api/publish-statuses/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, (await factory.Repository.GetAllAsync()).Count());
    }

    /// <summary>
    /// Over-gating is as wrong as under-gating: the content features are the daily work of an
    /// ordinary signed-in user, so none of them may acquire an Admin requirement. This is the
    /// regression guard for the four controllers the audit deliberately left open.
    /// </summary>
    [Fact]
    public async Task ContentEndpoints_WithoutTheAdminRole_StillReturn200()
    {
        using (var factory = new CourseApiFactory())
        {
            factory.TokenRoles = ["User"];
            using var client = factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/courses")).StatusCode);
        }

        using (var factory = new PartnerApiFactory())
        {
            factory.TokenRoles = ["User"];
            using var client = factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/partners")).StatusCode);
        }

        using (var factory = new CourseGroupApiFactory())
        {
            factory.TokenRoles = ["User"];
            using var client = factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/course-groups")).StatusCode);
        }

        using (var factory = new FeaturedPromoItemApiFactory())
        {
            factory.TokenRoles = ["User"];
            using var client = factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/featured-promo-items")).StatusCode);
        }
    }

    /// <summary>
    /// <c>ActiveAccountEvents</c> rejects a correctly-signed token that carries no <c>userId</c>
    /// claim: this API always writes one, so a token without it is not one we issued, and guessing
    /// an identity from <c>sub</c> would be exactly the wrong recovery.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_WithATokenMissingTheUserIdClaim_Returns401()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", IssueTokenWithoutUserIdClaim());

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/app-roles")).StatusCode);
    }

    /// <summary>
    /// <c>ClockSkew</c> is zero in both directions: a token whose <c>nbf</c> is still in the
    /// future is rejected now, not five minutes early.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_WithANotYetValidToken_Returns401()
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokens.Issue(issuedAt: DateTimeOffset.UtcNow.AddMinutes(10)));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/app-roles")).StatusCode);
    }

    /// <summary>
    /// An unusable <c>appConfig.symmetricSecurityKey</c> is a broken deployment, not a bad
    /// credential: the request fails as a <c>500</c> rather than a <c>401</c>, which would send a
    /// perfectly valid session back to the login page for a fault only an operator can fix.
    /// <para>
    /// Three shapes, one rule (<c>JwtSigningKey.ReadAsync</c>): no <c>appConfig</c> row at all, a
    /// blank key, and a key under the 256-bit HMAC-SHA256 floor.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    public async Task ProtectedEndpoint_WhenTheSigningKeyIsUnusable_Returns500(string? key)
    {
        using var factory = new AppRoleApiFactory();
        using var client = factory.CreateClient();

        factory.SysConfig.Config = key is null
            ? null
            : new AppConfig { SymmetricSecurityKey = key };

        var response = await client.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// A token correctly signed with the current key, but no <c>userId</c> claim — the shape
    /// <c>ActiveAccountEvents</c> has to catch. Built by hand because <see cref="TestTokens"/>
    /// goes through the production <c>JwtTokenService</c>, which always writes one.
    /// </summary>
    private static string IssueTokenWithoutUserIdClaim()
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(FakeSysConfigRepository.SymmetricSecurityKey));
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            claims: [new Claim(JwtRegisteredClaimNames.Sub, TestTokens.DefaultUserId)],
            notBefore: now,
            expires: now.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ProblemDetailsDto
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
    }
}
