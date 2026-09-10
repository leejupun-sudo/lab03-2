using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>
/// Hosts the real API pipeline for the <c>AuthController</c> suites with the clock pinned, so the
/// issued token's expiry is exact.
/// <para>
/// The accounts and the signing key both come from <see cref="ApiFactory"/> — the base seeds them
/// for every suite now that <c>ActiveAccountEvents</c> re-reads the account on each request.
/// <see cref="Repository"/> is the same fake under the name the login and profile tests already
/// use.
/// </para>
/// </summary>
public class AuthApiFactory : ApiFactory
{
    /// <summary>The instant every token in these tests is issued at.</summary>
    public static readonly DateTimeOffset IssuedAt = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The seeded accounts, under the name these suites use:
    ///   <c>miles@uuu.com.tw</c> active Admin+User, <c>helen</c> inactive, <c>Jenny_Tsao</c> roleless.
    /// </summary>
    public FakeAuthRepository Repository => Accounts;

    public FixedTimeProvider Clock { get; } = new(IssuedAt);

    protected override void ConfigureFakes(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Clock);
    }
}
