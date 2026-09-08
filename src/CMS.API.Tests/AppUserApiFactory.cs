using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>
/// Hosts the real API pipeline with the SQL-backed repositories swapped for
/// <see cref="FakeAppUserRepository"/> and <see cref="FakeSysConfigRepository"/>, so the
/// endpoint tests need no database and the default password is a known constant.
/// </summary>
public class AppUserApiFactory : WebApplicationFactory<Program>
{
    // Seeded out of UserId order so `ORDER BY UserId` is actually exercised:
    //
    //   pkid 1  miles@uuu.com.tw  — active, Admin + User; the ordinary row.
    //   pkid 2  helen             — INACTIVE, User only, PasswordUpdatedTime set; the tri-state,
    //                               role filter and date range all single it out.
    //   pkid 3  Jenny_Tsao        — active, NO roles; shares UserName "Helen" with pkid 2 so the
    //                               no-duplicate-check-on-UserName rule is proven.
    public FakeAppUserRepository Repository { get; } = new FakeAppUserRepository()
        .Seed("miles@uuu.com.tw", "Miles Sun", isActive: true, passwordUpdatedTime: null, "Admin", "User")
        .Seed("helen", "Helen", isActive: false, passwordUpdatedTime: new DateTime(2026, 3, 1, 9, 30, 0), "User")
        .Seed("Jenny_Tsao", "Helen", isActive: true, passwordUpdatedTime: null);

    public FakeSysConfigRepository SysConfig { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAppUserRepository>();
            services.AddSingleton<IAppUserRepository>(Repository);
            services.RemoveAll<ISysConfigRepository>();
            services.AddSingleton<ISysConfigRepository>(SysConfig);
        });
    }
}
