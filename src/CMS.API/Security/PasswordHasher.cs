using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Security;

/// <summary>
/// Produces the <c>AppUser.PasswordHash</c> representation.
/// <para>
/// SHA-256 over the UTF-8 bytes, rendered as 64 lowercase hex characters. This is not a
/// design choice — it is the format the one live row already uses (verified against
/// SysConfig's default password; uppercase hex, Base64 and UTF-16 input all fail to match),
/// so anything else would create accounts the existing login path cannot verify.
/// </para>
/// </summary>
public static class PasswordHasher
{
    public static string Sha256Hex(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
