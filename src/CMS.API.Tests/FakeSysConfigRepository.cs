using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>
/// In-memory stand-in for <see cref="ISysConfigRepository"/>. Holds one <c>appConfig</c>
/// value; set <see cref="Config"/> to <c>null</c> to simulate the row being absent.
/// </summary>
public class FakeSysConfigRepository : ISysConfigRepository
{
    public const string DefaultPassword = "P@ssw0rd!";

    /// <summary>JWT signing key — 33 characters, comfortably over the 32-byte HMAC-SHA256 floor.</summary>
    public const string SymmetricSecurityKey = "test-signing-key-0123456789abcdef";

    public AppConfig? Config { get; set; } = new()
    {
        DefaultPassword = DefaultPassword,
        EnforcePasswordPolicy = true,
        SymmetricSecurityKey = SymmetricSecurityKey
    };

    public Task<AppConfig?> GetAppConfigAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Config);
}
