using CMS.API.Repositories;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CMS.API.Security;

/// <summary>
/// Supplies the bearer middleware's signing key from the database on each request.
/// <para>
/// <c>JwtBearerOptions</c> is built once at startup, but the key lives in
/// <c>SysConfig.configValue</c> and is read per token when one is issued
/// (<see cref="JwtTokenService"/>). <c>IConfigurationManager&lt;OpenIdConnectConfiguration&gt;</c>
/// is the one extension point <c>JwtBearerHandler</c> awaits per request, so it is what keeps
/// validation on the same footing: rotate the row and the very next request validates against
/// the new key, with no redeploy and no restart. The alternative,
/// <c>TokenValidationParameters.IssuerSigningKeyResolver</c>, is synchronous and would force
/// a blocking wait on the async repository.
/// </para>
/// <para>
/// Only <c>SigningKeys</c> is populated. No issuer and no audience are declared, because the
/// tokens carry neither (<c>spec/auth/Login.md</c>) and the validation parameters switch both
/// checks off.
/// </para>
/// <para>
/// Nothing is cached, which costs one single-row <c>SysConfig</c> seek per authenticated
/// request in exchange for instant rotation. If that ever shows up in a profile, cache here —
/// it is the only place the key is read for validation.
/// </para>
/// </summary>
public sealed class SysConfigSigningKeys : IConfigurationManager<OpenIdConnectConfiguration>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SysConfigSigningKeys(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        // ISysConfigRepository is scoped (it opens a connection per call). The scope is our own
        // because IConfigurationManager<T> is a singleton with no HttpContext to resolve from —
        // not because the middleware runs outside scope resolution: ActiveAccountEvents, in this
        // same pipeline stage, resolves from context.HttpContext.RequestServices.
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISysConfigRepository>();

        var configuration = new OpenIdConnectConfiguration();
        configuration.SigningKeys.Add(await JwtSigningKey.ReadAsync(repository, cancel));

        return configuration;
    }

    /// <summary>No-op — nothing is cached, so every request already re-reads the row.</summary>
    public void RequestRefresh()
    {
    }
}
