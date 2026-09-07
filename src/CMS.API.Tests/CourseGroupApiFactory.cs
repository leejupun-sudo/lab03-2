using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>
/// Hosts the real API pipeline with the SQL-backed repository swapped for
/// <see cref="FakeCourseGroupRepository"/>, so the endpoint tests need no database.
/// </summary>
public class CourseGroupApiFactory : WebApplicationFactory<Program>
{
    // pkid 1 is referenced (delete guard blocks it); pkid 2 and 3 are free to delete.
    // pkid 1 and 3 deliberately share a Description — the live table holds duplicates
    // (215 rows / 213 distinct values), so the API must not reject them.
    public FakeCourseGroupRepository Repository { get; } = new FakeCourseGroupRepository()
        .Seed("Azure系列課程", courseCount: 48, partnerCourseGroupCount: 2)
        .Seed("SharePoint系列課程")
        .Seed("Azure系列課程");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICourseGroupRepository>();
            services.AddSingleton<ICourseGroupRepository>(Repository);
        });
    }
}
