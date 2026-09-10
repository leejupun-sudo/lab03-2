using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CMS.API.Models;
using Xunit;

namespace CMS.API.Tests;

/// <summary>
/// <c>PUT /api/auth/profile</c> — the signed-in user renaming themselves.
/// <para>
/// The rule under test is narrow and worth stating once: the account written is the one in the
/// <b>token</b>, the only column written is <c>UserName</c>, and nothing a caller can put in the
/// request body changes either of those.
/// </para>
/// </summary>
public class AuthProfileTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string TokenUser = "miles@uuu.com.tw";
    private const string OtherUser = "Jenny_Tsao";
    private const string ProfileUrl = "/api/auth/profile";

    // ---------- The happy path ----------

    [Fact]
    public async Task UpdateProfile_UpdatesUserNameForTheTokenUser()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName = "孫小明" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);
        Assert.Equal(TokenUser, body!.UserId);
        Assert.Equal("孫小明", body.UserName);
        Assert.Equal("孫小明", factory.Repository.Row(TokenUser).UserName);
    }

    /// <summary>Two properties, same containment rule as <c>LoginResponse</c>: no hash, no token.</summary>
    [Fact]
    public async Task UpdateProfile_ResponseHasOnlyUserIdAndUserName()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName = "Miles S." });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = document.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["userId", "userName"], names);
    }

    /// <summary>The identity comes from the token, so a different token renames a different row.</summary>
    [Fact]
    public async Task UpdateProfile_FollowsTheTokenIdentity_NotAFixedAccount()
    {
        using var factory = new AuthApiFactory { TokenUserId = OtherUser };
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName = "曹小珍" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("曹小珍", factory.Repository.Row(OtherUser).UserName);
        Assert.Equal("Miles Sun", factory.Repository.Row(TokenUser).UserName);
    }

    /// <summary>`UserId` matching follows the column's CI collation, exactly as login does.</summary>
    [Fact]
    public async Task UpdateProfile_MatchesTheAccountCaseInsensitively()
    {
        using var factory = new AuthApiFactory { TokenUserId = "MILES@UUU.COM.TW" };
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName = "Miles Upper" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Miles Upper", factory.Repository.Row(TokenUser).UserName);
    }

    [Fact]
    public async Task UpdateProfile_TrimsTheUserName()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName = "  Miles Sun  " });

        var body = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);
        Assert.Equal("Miles Sun", body!.UserName);
        Assert.Equal("Miles Sun", factory.Repository.Row(TokenUser).UserName);
    }

    // ---------- The body cannot choose the account ----------

    /// <summary>
    /// A <c>userId</c> in the request body is ignored: <see cref="UpdateProfileRequest"/> has no
    /// such property, so it never binds, and the account written stays the token's.
    /// </summary>
    [Fact]
    public async Task UpdateProfile_IgnoresAUserIdInTheRequestBody()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new
        {
            userId = OtherUser,
            userName = "Renamed By Body"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The token's row moved; the account named in the body did not.
        var body = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);
        Assert.Equal(TokenUser, body!.UserId);
        Assert.Equal("Renamed By Body", factory.Repository.Row(TokenUser).UserName);
        Assert.Equal("Jenny Tsao", factory.Repository.Row(OtherUser).UserName);
    }

    /// <summary>Roles, <c>IsActive</c> and the password hash are not this endpoint's to move.</summary>
    [Fact]
    public async Task UpdateProfile_ChangesNothingButTheUserName()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var before = factory.Repository.Row(TokenUser);
        var rolesBefore = before.RoleIds.ToArray();
        var hashBefore = before.PasswordHash;

        var response = await client.PutAsJsonAsync(ProfileUrl, new
        {
            userId = OtherUser,
            userName = "Miles Renamed",
            roleIds = new[] { "SuperUser" },
            isActive = false,
            passwordHash = "0000000000000000000000000000000000000000000000000000000000000000"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = factory.Repository.Row(TokenUser);
        Assert.Equal("Miles Renamed", after.UserName);
        Assert.Equal(rolesBefore, after.RoleIds);
        Assert.True(after.IsActive);
        Assert.Equal(hashBefore, after.PasswordHash);
    }

    // ---------- Validation ----------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t  ")]
    public async Task UpdateProfile_WithBlankUserName_Returns400AndWritesNothing(string userName)
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCount);
        Assert.Equal("Miles Sun", factory.Repository.Row(TokenUser).UserName);
    }

    [Fact]
    public async Task UpdateProfile_WithNoUserNameProperty_Returns400AndWritesNothing()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userId = TokenUser });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCount);
    }

    /// <summary><c>AppUser.UserName</c> is <c>nvarchar(200)</c>.</summary>
    [Fact]
    public async Task UpdateProfile_WithOverlongUserName_Returns400()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName = new string('X', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCount);
    }

    // ---------- Authorization ----------

    /// <summary>
    /// The regression this endpoint exists under: <c>[AllowAnonymous]</c> moved from
    /// <c>AuthController</c> to its login action precisely so this request needs a token. A
    /// class-level opt-out would leave the endpoint open with no way for an <c>[Authorize]</c>
    /// here to close it.
    /// </summary>
    [Fact]
    public async Task UpdateProfile_WithoutAToken_Returns401AndWritesNothing()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName = "Anonymous Rename" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
        Assert.Equal(0, factory.Repository.UpdateCount);
        Assert.Equal("Miles Sun", factory.Repository.Row(TokenUser).UserName);
    }

    [Fact]
    public async Task UpdateProfile_WithAGarbageToken_Returns401()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer not-a-token");

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName = "Forged" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCount);
    }

    /// <summary>Moving the attribute must not have taken the login endpoint's opt-out with it.</summary>
    [Fact]
    public async Task Login_IsStillReachableWithoutAToken()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { userId = TokenUser, password = AuthApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- The account is gone ----------

    /// <summary>
    /// A still-valid token for an account an admin has since deleted is turned away by the bearer
    /// middleware, before the controller runs.
    /// <para>
    /// This used to be the controller's own <c>404 查無使用者</c>: the token authenticated on its
    /// signature alone, the UPDATE matched no row, and the caller was told so. <c>ActiveAccountEvents</c>
    /// now re-reads the account on every request, so the same token no longer authenticates at all
    /// — a <c>401</c>, which is both earlier and the answer that actually ends the session (the
    /// Angular interceptor clears storage and returns to the login page on 401, not on 404).
    /// </para>
    /// <para>
    /// The controller keeps its 404 branch: it is now reachable only if the row is deleted in the
    /// window between that check and the UPDATE, and turning that race into a false 200 would be
    /// worse than answering it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task UpdateProfile_ForAnAccountThatNoLongerExists_Returns401FromTheMiddleware()
    {
        using var factory = new AuthApiFactory { TokenUserId = "ghost@uuu.com.tw" };
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName = "Ghost" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCount);
    }

    /// <summary>
    /// An account deactivated while its token is still in flight loses access on the very next
    /// request — the 啟用 checkbox is a real control, not a label.
    /// </summary>
    [Fact]
    public async Task UpdateProfile_AfterTheAccountIsDeactivated_Returns401AndWritesNothing()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsJsonAsync(ProfileUrl, new { userName = "Miles" })).StatusCode);

        factory.Repository.SetActive(TokenUser, false);

        var response = await client.PutAsJsonAsync(ProfileUrl, new { userName = "Renamed After Lockout" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Miles", factory.Repository.Row(TokenUser).UserName);
    }

    private class ProblemDetailsDto
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
    }
}
