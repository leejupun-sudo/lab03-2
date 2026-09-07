using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>Hosts the API with <see cref="ILookupRepository"/> swapped for an in-memory fake.</summary>
public class LookupApiFactory : WebApplicationFactory<Program>
{
    public FakeLookupRepository Repository { get; } = new FakeLookupRepository()
        .SeedUser("helen", "helen")
        .SeedUser("miles@uuu.com.tw", "Miles Sun")
        .SeedPublishStatus(1, "草稿")
        .SeedPublishStatus(2, "上架中")
        .SeedPublishStatus(3, "已下架")
        // Seeded out of alphabetical order so the ORDER BY Description is actually exercised.
        .SeedCourseGroup(7, "SharePoint系列課程")
        .SeedCourseGroup(3, "Azure系列課程")
        .SeedCourseGroup(5, "PMI®專案管理認證系列");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILookupRepository>();
            services.AddSingleton<ILookupRepository>(Repository);
        });
    }
}
