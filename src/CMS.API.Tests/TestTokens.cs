using CMS.API.Models;
using CMS.API.Security;

namespace CMS.API.Tests;

/// <summary>
/// Issues access tokens for the endpoint tests through the <b>production</b>
/// <see cref="JwtTokenService"/>, so a test can never authenticate with a token shaped
/// differently from the one <c>POST /api/auth/login</c> hands out.
/// </summary>
public static class TestTokens
{
    /// <summary>The identity <see cref="ApiFactory"/> authenticates as unless a test says otherwise.</summary>
    public const string DefaultUserId = "miles@uuu.com.tw";

    /// <summary>Name on the default identity.</summary>
    public const string DefaultUserName = "Miles Sun";

    /// <summary>Roles on the default identity.</summary>
    public static readonly string[] DefaultRoles = ["Admin", "User"];

    /// <summary>
    /// Signs a token for the given identity.
    /// </summary>
    /// <param name="signingKey">
    /// Defaults to <see cref="FakeSysConfigRepository.SymmetricSecurityKey"/> — the key the hosted
    /// API validates against. Pass another to produce a token whose signature must be rejected.
    /// </param>
    /// <param name="issuedAt">
    /// Defaults to now. Pass an instant more than 24 h in the past for an expired token, or one in
    /// the future for a not-yet-valid one.
    /// </param>
    public static string Issue(
        string userId = DefaultUserId,
        string? userName = null,
        IEnumerable<string>? roles = null,
        string signingKey = FakeSysConfigRepository.SymmetricSecurityKey,
        DateTimeOffset? issuedAt = null)
    {
        var sysConfig = new FakeSysConfigRepository
        {
            Config = new AppConfig { SymmetricSecurityKey = signingKey }
        };
        var clock = new FixedTimeProvider(issuedAt ?? DateTimeOffset.UtcNow);
        var credential = new AppUserCredential
        {
            UserId = userId,
            UserName = userName ?? DefaultUserName,
            IsActive = true,
            RoleIds = [.. roles ?? DefaultRoles]
        };

        return new JwtTokenService(sysConfig, clock)
            .CreateAccessTokenAsync(credential)
            .GetAwaiter()
            .GetResult();
    }
}
