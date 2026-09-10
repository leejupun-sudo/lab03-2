using CMS.API.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>
/// Hosts the real API pipeline (routing, model binding, validation, JSON) with the
/// SQL-backed repository swapped for <see cref="FakeAppRoleRepository"/>, so the
/// endpoint tests need no database.
/// </summary>
public class AppRoleApiFactory : ApiFactory
{
    public FakeAppRoleRepository Repository { get; } = new FakeAppRoleRepository()
        .Seed("Admin", "Administrator", 1, "系統管理員", "helen", "Jenny_Tsao", "miles@uuu.com.tw")
        .Seed("User", "User", 100, "一般使用者", "bob");

    protected override void ConfigureFakes(IServiceCollection services)
    {
        services.RemoveAll<IAppRoleRepository>();
        services.AddSingleton<IAppRoleRepository>(Repository);
    }
}
