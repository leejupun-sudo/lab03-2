using System.Net.Http.Headers;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>
/// Base for every endpoint-test factory. Hosts the real API pipeline — routing, model binding,
/// validation, JSON casing <i>and now authentication</i> — with the SQL-backed repositories
/// swapped for in-memory fakes, so the tests need no database.
/// <para>
/// Since <c>Program.cs</c> applies <c>RequireAuthorization()</c> to the whole controller surface,
/// three things must be true of every suite: <see cref="ISysConfigRepository"/> is faked here (the
/// bearer middleware reads its signing key from that row on each request, and a test must not go
/// to SQL Server for it), <see cref="IAuthRepository"/> is faked here too — <c>ActiveAccountEvents</c>
/// re-reads the account on every request for exactly the same reason — and
/// <see cref="CreateClient"/> arrives carrying a valid token. Tests that want the unauthenticated
/// behaviour ask for <see cref="CreateAnonymousClient"/>.
/// </para>
/// </summary>
public abstract class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Backs both halves of the token's life in these tests: <c>JwtTokenService</c> signs with
    /// this key and the bearer middleware validates against it.
    /// </summary>
    public FakeSysConfigRepository SysConfig { get; } = new();

    /// <summary>The password every seeded account uses.</summary>
    public const string Password = "CMS4fun#";

    /// <summary>
    /// The accounts the bearer middleware resolves tokens against, for every suite.
    /// <para>
    /// <c>ActiveAccountEvents</c> re-checks the account on each request, so a token now needs a
    /// row behind it or nothing authenticates — the same reason <see cref="SysConfig"/> is faked
    /// here. The three identities cover what the suites need:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>miles@uuu.com.tw</c> — active, Admin + User. The default; reaches the 系統管理 endpoints.</item>
    ///   <item><c>helen</c> — <b>inactive</b>, User. Correct password, still rejected.</item>
    ///   <item><c>Jenny_Tsao</c> — active, <b>no roles</b>. Authenticates, but 403s on an Admin-only endpoint.</item>
    /// </list>
    /// </summary>
    public FakeAuthRepository Accounts { get; } = new FakeAuthRepository()
        .Seed("miles@uuu.com.tw", "Miles Sun", Password, isActive: true, "User", "Admin")
        .Seed("helen", "Helen", Password, isActive: false, "User")
        .Seed("Jenny_Tsao", "Jenny Tsao", Password, isActive: true);

    /// <summary>Account the default client authenticates as. Set before <see cref="CreateClient"/>.</summary>
    public string TokenUserId { get; set; } = TestTokens.DefaultUserId;

    /// <summary>Name on the default client's token.</summary>
    public string TokenUserName { get; set; } = TestTokens.DefaultUserName;

    /// <summary>Roles on the default client's token.</summary>
    public IReadOnlyList<string> TokenRoles { get; set; } = TestTokens.DefaultRoles;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISysConfigRepository>();
            services.AddSingleton<ISysConfigRepository>(SysConfig);
            services.RemoveAll<IAuthRepository>();
            services.AddSingleton<IAuthRepository>(Accounts);
            ConfigureFakes(services);
        });
    }

    /// <summary>Swap in the repositories this suite needs. Called after the shared fakes are registered.</summary>
    protected abstract void ConfigureFakes(IServiceCollection services);

    /// <summary>Every client from <see cref="CreateClient"/> is signed in.</summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Issue(TokenUserId, TokenUserName, TokenRoles));
    }

    /// <summary>A client with no <c>Authorization</c> header — for asserting the 401.</summary>
    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }
}
