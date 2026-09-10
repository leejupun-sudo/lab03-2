using System.Text;
using CMS.API.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

/// <summary>
/// The single place the JWT signing key is read.
/// <para>
/// Both halves of the token's life go through here — <see cref="JwtTokenService"/> when it
/// signs one, and <see cref="SysConfigSigningKeys"/> when the bearer middleware validates one —
/// so the issuing key and the validating key cannot drift apart, and the 256-bit floor is
/// enforced once instead of twice.
/// </para>
/// <para>
/// The value comes from <c>SysConfig.configValue</c> (<c>configKey = 'appConfig'</c>) at
/// runtime, never from appsettings and never hard-coded, so editing that row rotates the key
/// with no redeploy.
/// </para>
/// </summary>
public static class JwtSigningKey
{
    /// <summary>HMAC-SHA256 需要至少 256 位元 (32 位元組) 的金鑰.</summary>
    public const int MinimumKeyBytes = 32;

    /// <summary>
    /// Reads <c>appConfig.symmetricSecurityKey</c> and wraps it as an HMAC key.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The <c>appConfig</c> row is missing, the key is blank, or it is shorter than
    /// <see cref="MinimumKeyBytes"/>. Each of those is a deployment fault, not a credential
    /// problem — see <c>spec/auth/Login.md</c>.
    /// </exception>
    public static async Task<SymmetricSecurityKey> ReadAsync(
        ISysConfigRepository sysConfigRepository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sysConfigRepository);

        var config = await sysConfigRepository.GetAppConfigAsync(cancellationToken);
        if (config is null || string.IsNullOrWhiteSpace(config.SymmetricSecurityKey))
        {
            throw new InvalidOperationException(
                "SysConfig 'appConfig' is missing or has no symmetricSecurityKey; cannot sign or validate an access token.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(config.SymmetricSecurityKey);
        if (keyBytes.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"SysConfig 'appConfig'.symmetricSecurityKey is {keyBytes.Length} bytes; " +
                $"HMAC-SHA256 requires at least {MinimumKeyBytes}.");
        }

        return new SymmetricSecurityKey(keyBytes);
    }
}
