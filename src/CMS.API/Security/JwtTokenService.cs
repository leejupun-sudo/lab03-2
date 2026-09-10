using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

public interface IJwtTokenService
{
    /// <summary>
    /// 以 SysConfig 的 <c>symmetricSecurityKey</c> 簽發 24 小時有效的 access token.
    /// </summary>
    Task<string> CreateAccessTokenAsync(AppUserCredential user, CancellationToken cancellationToken = default);
}

/// <summary>
/// Issues the login access token.
/// <para>
/// The signing key is read from <c>SysConfig.configValue</c> (<c>configKey = 'appConfig'</c>) on
/// every call — never from appsettings and never hard-coded — so rotating the row rotates the key
/// without a redeploy.
/// </para>
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    /// <summary>Token 有效期 — 簽發後 24 小時.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    /// <summary>帳號 claim (除了標準的 <c>sub</c> 之外另外帶一個好讀的名稱).</summary>
    public const string UserIdClaimType = "userId";

    /// <summary>姓名 claim.</summary>
    public const string UserNameClaimType = "userName";

    /// <summary>角色 claim — 每個 AppUserRole.RoleId 一個.</summary>
    public const string RoleClaimType = "role";

    private readonly ISysConfigRepository _sysConfigRepository;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(ISysConfigRepository sysConfigRepository, TimeProvider timeProvider)
    {
        _sysConfigRepository = sysConfigRepository;
        _timeProvider = timeProvider;
    }

    public async Task<string> CreateAccessTokenAsync(
        AppUserCredential user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var signingKey = await SigningKeyAsync(cancellationToken);
        var issuedAt = _timeProvider.GetUtcNow().UtcDateTime;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(UserIdClaimType, user.UserId),
            new(UserNameClaimType, user.UserName)
        };

        // One claim per AppUserRole row. A user with no roles simply gets none.
        claims.AddRange(user.RoleIds.Select(roleId => new Claim(RoleClaimType, roleId)));

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: issuedAt,
            expires: issuedAt.Add(TokenLifetime),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// The symmetric key from SysConfig, read through the shared <see cref="JwtSigningKey"/> so the
    /// signing key and the bearer middleware's validating key are provably the same value.
    /// </summary>
    private Task<SymmetricSecurityKey> SigningKeyAsync(CancellationToken cancellationToken) =>
        JwtSigningKey.ReadAsync(_sysConfigRepository, cancellationToken);
}
