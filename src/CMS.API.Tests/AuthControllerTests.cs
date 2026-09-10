using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CMS.API.Models;
using CMS.API.Security;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CMS.API.Tests;

public class AuthControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string ActiveUser = "miles@uuu.com.tw";
    private const string InactiveUser = "helen";
    private const string RolelessUser = "Jenny_Tsao";
    private const string Password = AuthApiFactory.Password;

    // ---------- Success ----------

    [Fact]
    public async Task Login_WithValidActiveUser_ReturnsProfileAndToken()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await PostLoginAsync(client, ActiveUser, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(ActiveUser, body!.UserId);
        Assert.Equal("Miles Sun", body.UserName);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));

        // Three segments: a real signed JWS, not a placeholder string.
        Assert.Equal(3, body.AccessToken.Split('.').Length);
    }

    /// <summary>The response object carries exactly three properties — nothing else leaks.</summary>
    [Fact]
    public async Task Login_ResponseHasOnlyUserIdUserNameAndAccessToken()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var json = await (await PostLoginAsync(client, ActiveUser, Password)).Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["accessToken", "userId", "userName"], names);
    }

    // ---------- Rejections ----------

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await PostLoginAsync(client, ActiveUser, "not-the-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUserId_Returns401()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await PostLoginAsync(client, "nobody@uuu.com.tw", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Correct password, but IsActive = 0 — still rejected.</summary>
    [Fact]
    public async Task Login_WithInactiveUser_Returns401()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await PostLoginAsync(client, InactiveUser, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The three failures must be indistinguishable: same status, same body. Anything else tells
    /// an attacker which accounts exist or which are merely disabled.
    /// </summary>
    [Fact]
    public async Task Login_AllFailureReasons_ReturnTheSameGenericBody()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var attempts = new[]
        {
            (UserId: ActiveUser, Password: "not-the-password"),
            (UserId: "nobody@uuu.com.tw", Password: Password),
            (UserId: InactiveUser, Password: Password)
        };

        var bodies = new List<string>();
        foreach (var attempt in attempts)
        {
            var response = await PostLoginAsync(client, attempt.UserId, attempt.Password);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            bodies.Add(await response.Content.ReadAsStringAsync());
        }

        Assert.Single(bodies.Distinct());

        var problem = JsonSerializer.Deserialize<ProblemDetailsDto>(bodies[0], JsonOptions);
        Assert.Equal("登入失敗", problem!.Title);
        Assert.Equal("帳號或密碼錯誤。", problem.Detail);

        // The generic message must not name the account or the failing check.
        foreach (var forbidden in new[] { ActiveUser, InactiveUser, "IsActive", "PasswordHash" })
        {
            Assert.DoesNotContain(forbidden, bodies[0], StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>A rejected login returns no token at all, not an empty one.</summary>
    [Fact]
    public async Task Login_WhenRejected_ReturnsNoAccessToken()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var json = await (await PostLoginAsync(client, ActiveUser, "wrong")).Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("accessToken", out _));
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData(ActiveUser, "")]
    public async Task Login_WithMissingField_Returns400(string userId, string password)
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await PostLoginAsync(client, userId, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.LookupCount);
    }

    // ---------- The PasswordHash contract ----------

    /// <summary>
    /// The hash must not appear anywhere the client can see it — not in the response body and not
    /// in the token payload, which is only Base64-encoded, not encrypted.
    /// </summary>
    [Fact]
    public async Task Login_NeverExposesPasswordHash()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await PostLoginAsync(client, ActiveUser, Password);
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<LoginResponse>(json, JsonOptions)!;

        var expectedHash = PasswordHasher.Sha256Hex(Password);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(expectedHash, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, json, StringComparison.Ordinal);

        var payload = DecodePayload(body.AccessToken);
        Assert.DoesNotContain("passwordHash", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(expectedHash, payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, payload, StringComparison.Ordinal);
    }

    // ---------- Token claims ----------

    [Fact]
    public async Task Login_TokenCarriesUserIdAndUserNameClaims()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var token = await ReadTokenAsync(client, ActiveUser, Password);

        Assert.Equal(ActiveUser, ClaimValue(token, JwtTokenService.UserIdClaimType));
        Assert.Equal("Miles Sun", ClaimValue(token, JwtTokenService.UserNameClaimType));
        Assert.Equal(ActiveUser, ClaimValue(token, JwtRegisteredClaimNames.Sub));
    }

    /// <summary>Every RoleId in AppUserRole becomes a role claim.</summary>
    [Fact]
    public async Task Login_TokenCarriesEveryRoleIdAsARoleClaim()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var token = await ReadTokenAsync(client, ActiveUser, Password);

        var roles = token.Claims
            .Where(c => c.Type == JwtTokenService.RoleClaimType)
            .Select(c => c.Value)
            .OrderBy(v => v, StringComparer.Ordinal);
        Assert.Equal(["Admin", "User"], roles);
    }

    [Fact]
    public async Task Login_UserWithNoRoles_GetsATokenWithNoRoleClaims()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var token = await ReadTokenAsync(client, RolelessUser, Password);

        Assert.Equal(RolelessUser, ClaimValue(token, JwtTokenService.UserIdClaimType));
        Assert.DoesNotContain(token.Claims, c => c.Type == JwtTokenService.RoleClaimType);
    }

    // ---------- Token lifetime ----------

    /// <summary>The token expires 24 hours after issue. The clock is pinned, so this is exact.</summary>
    [Fact]
    public async Task Login_TokenExpiresTwentyFourHoursAfterIssue()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var token = await ReadTokenAsync(client, ActiveUser, Password);

        var issuedAt = AuthApiFactory.IssuedAt.UtcDateTime;
        Assert.Equal(issuedAt, token.ValidFrom);
        Assert.Equal(issuedAt.AddHours(24), token.ValidTo);
        Assert.Equal(TimeSpan.FromHours(24), token.ValidTo - token.ValidFrom);
    }

    // ---------- Signing key ----------

    /// <summary>
    /// The signature must verify against the key held in SysConfig — proof the secret is read from
    /// the database row rather than a constant. The key used here is not the production one.
    /// </summary>
    [Fact]
    public async Task Login_TokenIsSignedWithTheSysConfigKey()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var raw = await ReadAccessTokenAsync(client, ActiveUser, Password);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(
            raw, ValidationParameters(FakeSysConfigRepository.SymmetricSecurityKey), out _);

        Assert.Equal(ActiveUser, principal.FindFirst(JwtTokenService.UserIdClaimType)?.Value);

        // The same token must fail against any other key.
        Assert.ThrowsAny<SecurityTokenInvalidSignatureException>(() =>
            handler.ValidateToken(raw, ValidationParameters("a-completely-different-key-32ch!!"), out _));
    }

    /// <summary>Rotating the SysConfig row rotates the signing key with no redeploy.</summary>
    [Fact]
    public async Task Login_UsesTheCurrentSysConfigKey_WhenItChanges()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        const string rotated = "rotated-signing-key-0123456789abc";
        factory.SysConfig.Config!.SymmetricSecurityKey = rotated;

        var raw = await ReadAccessTokenAsync(client, ActiveUser, Password);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        handler.ValidateToken(raw, ValidationParameters(rotated), out _);
    }

    // ---------- The stored hash's own shapes (ship-workflow coverage audit) ----------

    /// <summary>
    /// A row whose <c>PasswordHash</c> is blank — an account created outside the API, or one whose
    /// column was cleared — must not become a password-less account. <c>HashMatches</c> short-
    /// circuits to <c>false</c> before the fixed-time compare, so the account's real password no
    /// longer opens it either. (A blank <i>supplied</i> password never gets this far: <c>Required</c>
    /// on <c>LoginRequest.Password</c> answers 400 first.)
    /// </summary>
    [Fact]
    public async Task Login_WithABlankStoredHash_Returns401()
    {
        using var factory = new AuthApiFactory();
        factory.Repository.SetRawHash(ActiveUser, string.Empty);
        using var client = factory.CreateClient();

        var response = await PostLoginAsync(client, ActiveUser, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The stored hash is lowercased before the comparison, so a row written in uppercase hex
    /// still verifies. Without that, a legacy row would lock its owner out with the same generic
    /// 401 as a wrong password — indistinguishable from the outside, and impossible to self-serve.
    /// </summary>
    [Fact]
    public async Task Login_WithAnUppercaseStoredHash_StillSucceeds()
    {
        using var factory = new AuthApiFactory();
        factory.Repository.SetRawHash(ActiveUser, PasswordHasher.Sha256Hex(Password).ToUpperInvariant());
        using var client = factory.CreateClient();

        var response = await PostLoginAsync(client, ActiveUser, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// <c>LoginRequest.UserId</c> is capped at 200 to match <c>AppUser.UserId</c>. An overlong
    /// value is a 400 from validation, and never reaches the repository — no lookup, no timing
    /// signal, no oversized parameter handed to Dapper.
    /// </summary>
    [Fact]
    public async Task Login_WithAnOverlongUserId_Returns400AndNeverQueries()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await PostLoginAsync(client, new string('x', 201), Password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.LookupCount);
    }

    // ---------- Helpers ----------

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string userId, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { userId, password });

    private static async Task<string> ReadAccessTokenAsync(HttpClient client, string userId, string password)
    {
        var response = await PostLoginAsync(client, userId, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        return body!.AccessToken;
    }

    private static async Task<JwtSecurityToken> ReadTokenAsync(HttpClient client, string userId, string password)
    {
        var raw = await ReadAccessTokenAsync(client, userId, password);

        // ReadJwtToken applies no inbound claim-type mapping, so the claim names read back are the
        // ones the service actually wrote.
        return new JwtSecurityTokenHandler().ReadJwtToken(raw);
    }

    private static string? ClaimValue(JwtSecurityToken token, string type) =>
        token.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    private static TokenValidationParameters ValidationParameters(string key) => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };

    private static string DecodePayload(string accessToken)
    {
        var segment = accessToken.Split('.')[1];
        var padded = segment.Replace('-', '+').Replace('_', '/').PadRight((segment.Length + 3) / 4 * 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    private class ProblemDetailsDto
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
    }
}
