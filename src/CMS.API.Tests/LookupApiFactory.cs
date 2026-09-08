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
        // Seeded out of RoleId order so the ORDER BY RoleId is actually exercised.
        .SeedRole(2, "User", "User")
        .SeedRole(1, "Admin", "Administrator")
        .SeedPublishStatus(1, "草稿")
        .SeedPublishStatus(2, "上架中")
        .SeedPublishStatus(3, "已下架")
        // Seeded out of alphabetical order so the ORDER BY Description is actually exercised.
        .SeedCourseGroup(7, "SharePoint系列課程")
        .SeedCourseGroup(3, "Azure系列課程")
        .SeedCourseGroup(5, "PMI®專案管理認證系列")
        // Seeded out of order, and the last two share DisplayOrder 9999 with the same Name —
        // exactly the live shape that makes the Name tie-break and the (AppKey) label necessary.
        .SeedPartner(31, "國際標準課程", "PCB", 9999)
        .SeedPartner(11, "CompTIA", "CompTIA", 3)
        .SeedPartner(19, "國際標準課程", "ISO", 9999)
        // Certifications seeded out of order; the Fortinet pair shares a partner so Title breaks the tie.
        // Titles carry nchar padding to prove the RTRIM reaches the wire.
        .SeedCertification(34, "FCP-SN     ", "Fortinet資安專家認證課程", 50, 123)
        .SeedCertification(5, "CCNA       ", "Cisco", 2, 4)
        .SeedCertification(36, "FCP-PCS    ", "Fortinet資安專家認證課程", 50, 123)
        .SeedJobCategory(24, "資訊安全 Security")
        .SeedJobCategory(1, "網路系統工程 System Engineer")
        .SeedJobCategory(16, "雲端技術 Cloud - Microsoft Azure")
        .SeedCourse(41, "IINS", "CCNA Security認證-建置Cisco網路安全")
        .SeedCourse(35, "PLF", "Oracle資料庫之PL／SQL基礎")
        .SeedCourse(2063, "14064GLV", "ISO 14064溫室氣體主導查證師／確證師訓練課程");

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
