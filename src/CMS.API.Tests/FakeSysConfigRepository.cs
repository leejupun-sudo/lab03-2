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

    public AppConfig? Config { get; set; } = new()
    {
        DefaultPassword = DefaultPassword,
        EnforcePasswordPolicy = true
    };

    public Task<AppConfig?> GetAppConfigAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Config);
}
