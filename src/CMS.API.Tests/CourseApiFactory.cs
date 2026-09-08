using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>
/// Hosts the real API pipeline with the SQL-backed repository swapped for
/// <see cref="FakeCourseRepository"/>, so the endpoint tests need no database.
/// </summary>
public class CourseApiFactory : WebApplicationFactory<Program>
{
    // The seed is chosen so every rule the spec records is actually exercised:
    //
    //   pkid 1  PLF     Oracle, group 18, 已下架 — referenced by FAQ + related links: the
    //                   ordinary delete-guard case. Has 2 certifications + 2 job categories.
    //   pkid 2  IINS    Cisco, group 14, 上架中, CanRepeat — referenced ONLY by CourseRecomm,
    //                   which declares no FK. Deleting it must still 409.
    //   pkid 3  AZ-900  Microsoft, NO course group, 草稿 — wholly unreferenced: the deletable row.
    //                   Shares its Title and FriendlyUrl with pkid 4 (both are legal duplicates).
    //   pkid 4  AZ-104  Microsoft, group 3, 上架中 — the Title/FriendlyUrl twin of pkid 3.
    //
    // CourseIds are seeded out of order so the CourseId ASC ordering is observable.
    public FakeCourseRepository Repository { get; } = new FakeCourseRepository()
        .Seed("PLF", "Oracle資料庫之PL／SQL基礎",
            partnerPkid: 2, partnerName: "Oracle", courseGroupPkid: 18, courseGroupDescription: "Oracle DB/My SQL資料庫系列課程",
            publishStatusPkid: 3, publishStatusDescription: "已下架",
            scheduleOn: new DateOnly(2015, 11, 10), scheduleOff: new DateOnly(2021, 11, 1),
            displayOrder: 600, hour: 12, listPrice: 49000, learningCredit: 14.0m,
            faqCount: 2, relatedLinkCount: 3,
            certificationPkids: [36, 34], jobCategoryPkids: [22, 19])
        .Seed("IINS", "CCNA Security認證-建置Cisco網路安全",
            partnerPkid: 4, partnerName: "Cisco", courseGroupPkid: 14, courseGroupDescription: "Cisco系列課程",
            publishStatusPkid: 2, publishStatusDescription: "上架中",
            scheduleOn: new DateOnly(2015, 11, 9), scheduleOff: new DateOnly(2020, 2, 24),
            displayOrder: 1, hour: 35, listPrice: 63000, learningCredit: 16.0m, canRepeat: true,
            recommCount: 5, jobCategoryPkids: [24])
        .Seed("AZ-900", "Azure基礎",
            partnerPkid: 1, partnerName: "Microsoft", courseGroupPkid: null, courseGroupDescription: null,
            publishStatusPkid: 1, publishStatusDescription: "草稿",
            scheduleOn: new DateOnly(2024, 6, 1), scheduleOff: new DateOnly(2034, 6, 1),
            displayOrder: 0, hour: 7, listPrice: 0, learningCredit: 0m,
            friendlyUrl: "Azure-Fundamentals", outline: "<p>Day 1</p>")
        .Seed("AZ-104", "Azure基礎",
            partnerPkid: 1, partnerName: "Microsoft", courseGroupPkid: 3, courseGroupDescription: "Azure系列課程",
            publishStatusPkid: 2, publishStatusDescription: "上架中",
            scheduleOn: new DateOnly(2024, 6, 1), scheduleOff: new DateOnly(2034, 6, 1),
            displayOrder: 2, hour: 28, listPrice: 42000, learningCredit: 12.5m,
            friendlyUrl: "Azure-Fundamentals", jobCategoryPkids: [16]);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICourseRepository>();
            services.AddSingleton<ICourseRepository>(Repository);
        });
    }
}
