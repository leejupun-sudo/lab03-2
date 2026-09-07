using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>
/// Hosts the real API pipeline with the SQL-backed repository swapped for
/// <see cref="FakePartnerRepository"/>, so the endpoint tests need no database.
/// </summary>
public class PartnerApiFactory : WebApplicationFactory<Program>
{
    // The seed is chosen so every rule the spec records is actually exercised:
    //
    //   pkid 1  CompTIA  — referenced by Course/Certification/PartnerCourseGroup/Promotion2;
    //                      the delete guard's ordinary case.
    //   pkid 2  國際標準課程 — referenced ONLY by Seminar, which declares no FK constraint.
    //                      Deleting it must still 409; this is the case SQL Server would allow.
    //   pkid 3  國際標準課程 — shares pkid 2's Name (the live table holds three such rows) and
    //                      is wholly unreferenced, so it is the deletable row.
    //   pkid 4  Aruba    — carries an ImageFilename, and DisplayOrder 3 puts it first.
    //
    // pkid 2 and 3 also share DisplayOrder 9999, so the Name ASC tie-break is observable.
    public FakePartnerRepository Repository { get; } = new FakePartnerRepository()
        .Seed("CompTIA", "CompTIA", displayOrder: 3, imageFilename: "CompTIA.png",
            courseCount: 42, certificationCount: 4, partnerCourseGroupCount: 2, promotion2Count: 7)
        .Seed("國際標準課程", "ISO", displayOrder: 9999, seminarCount: 34)
        .Seed("國際標準課程", "PCB", displayOrder: 9999)
        // No extension — 17 of the 62 non-null live values look like this.
        .Seed("Aruba", "Aruba", displayOrder: 3, imageFilename: "Splunk");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPartnerRepository>();
            services.AddSingleton<IPartnerRepository>(Repository);
        });
    }
}
