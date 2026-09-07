using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CMS.API.Models;
using Xunit;

namespace CMS.API.Tests;

public class PartnersControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ---------- List / filter ----------

    [Fact]
    public async Task GetAll_ReturnsAllPartners_OrderedByDisplayOrderThenName()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/partners");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var partners = await ReadPartnersAsync(response);

        // DisplayOrder 3 rows first; within each group Name ASC breaks the tie.
        Assert.Equal([(short)4, (short)1, (short)2, (short)3], partners.Select(p => p.Pkid));
    }

    [Fact]
    public async Task GetAll_IncludesAllFiveUsageCounts()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var partners = await ReadPartnersAsync(await client.GetAsync("/api/partners"));

        var used = partners.Single(p => p.Pkid == 1);
        Assert.Equal(42, used.CourseCount);
        Assert.Equal(4, used.CertificationCount);
        Assert.Equal(2, used.PartnerCourseGroupCount);
        Assert.Equal(7, used.Promotion2Count);
        Assert.Equal(0, used.SeminarCount);

        var seminarOnly = partners.Single(p => p.Pkid == 2);
        Assert.Equal(34, seminarOnly.SeminarCount);
        Assert.Equal(0, seminarOnly.CourseCount);

        var unused = partners.Single(p => p.Pkid == 3);
        Assert.Equal(0, unused.CourseCount + unused.CertificationCount
            + unused.PartnerCourseGroupCount + unused.Promotion2Count + unused.SeminarCount);
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesName()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var partners = await QueryAsync(client, new PartnerQuery { Keyword = "Aruba" });

        Assert.Equal((short)4, Assert.Single(partners).Pkid);
    }

    /// <summary>The keyword spans four columns, not just Name — AppKey is one of them.</summary>
    [Fact]
    public async Task Query_WithKeyword_MatchesAppKey()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var partners = await QueryAsync(client, new PartnerQuery { Keyword = "PCB" });

        Assert.Equal((short)3, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesNameOnPartnerMenu()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        // The fake seeds NameOnPartnerMenu as "<name>選單"; nothing else contains 選單.
        var partners = await QueryAsync(client, new PartnerQuery { Keyword = "CompTIA選單" });

        Assert.Equal((short)1, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_WithKeywordMatchingDuplicateNames_ReturnsBothRows()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var partners = await QueryAsync(client, new PartnerQuery { Keyword = "國際標準課程" });

        Assert.Equal([(short)2, (short)3], partners.Select(p => p.Pkid).Order());
    }

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllPartners()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var partners = await QueryAsync(client, new PartnerQuery());

        Assert.Equal(4, partners.Count);
    }

    [Fact]
    public async Task Query_WithNoMatch_ReturnsEmptyList()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var partners = await QueryAsync(client, new PartnerQuery { Keyword = "no-such-partner" });

        Assert.Empty(partners);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsThePartnerWithEveryField()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/partners/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var partner = await response.Content.ReadFromJsonAsync<Partner>(JsonOptions);
        Assert.NotNull(partner);
        Assert.Equal((short)1, partner.Pkid);
        Assert.Equal("CompTIA", partner.Name);
        Assert.Equal("CompTIA", partner.AppKey);
        Assert.Equal("CompTIA選單", partner.NameOnPartnerMenu);
        Assert.Equal("CompTIA", partner.NameOnCourseDetailPage);
        Assert.Equal(3, partner.DisplayOrder);
        Assert.Equal("CompTIA.png", partner.ImageFilename);
    }

    [Fact]
    public async Task GetById_RowWithNoImage_ReturnsNullImageFilename()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var partner = await (await client.GetAsync("/api/partners/2"))
            .Content.ReadFromJsonAsync<Partner>(JsonOptions);

        Assert.NotNull(partner);
        Assert.Null(partner.ImageFilename);
    }

    [Fact]
    public async Task GetById_UnknownPkid_Returns404()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/partners/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AboveSmallintRange_Returns400()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        // The route constraint is :int but the action takes a short — 40000 fails model binding.
        var response = await client.GetAsync("/api/partners/40000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_AssignsAnIdentityPkid_AndReturnsCreated()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/partners", Request(name: "Cisco", appKey: "Cisco"), JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Partner>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal((short)5, created.Pkid);
        Assert.Equal("Cisco", created.Name);
        Assert.Equal($"/api/partners/{created.Pkid}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidSuppliedInTheBody()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var request = Request(name: "Cisco", appKey: "Cisco");
        request.Pkid = 99;

        var response = await client.PostAsJsonAsync("/api/partners", request, JsonOptions);

        var created = await response.Content.ReadFromJsonAsync<Partner>(JsonOptions);
        Assert.Equal((short)5, created!.Pkid);
    }

    /// <summary>
    /// Name carries no UNIQUE constraint and the live table holds duplicates
    /// (66 rows / 64 distinct), so a repeat name must succeed rather than 409.
    /// </summary>
    [Fact]
    public async Task Create_WithANameThatAlreadyExists_Returns201NotConflict()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/partners", Request(name: "國際標準課程", appKey: "ISO27001"), JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var partners = await ReadPartnersAsync(await client.GetAsync("/api/partners"));
        Assert.Equal(3, partners.Count(p => p.Name == "國際標準課程"));
    }

    /// <summary>AppKey is distinct across all 66 live rows and is guarded as an app-level rule.</summary>
    [Fact]
    public async Task Create_WithAnAppKeyThatAlreadyExists_Returns409()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/partners", Request(name: "另一個 ISO", appKey: "ISO"), JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("應用代碼重複", problem?.Title);

        Assert.Equal(4, (await ReadPartnersAsync(await client.GetAsync("/api/partners"))).Count);
    }

    [Fact]
    public async Task Create_WithBlankImageFilename_StoresNull()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var request = Request(name: "Cisco", appKey: "Cisco");
        request.ImageFilename = "   ";

        var created = await (await client.PostAsJsonAsync("/api/partners", request, JsonOptions))
            .Content.ReadFromJsonAsync<Partner>(JsonOptions);

        Assert.Null(created!.ImageFilename);
    }

    /// <summary>17 of the 62 non-null live values carry no extension — no format rule applies.</summary>
    [Fact]
    public async Task Create_WithAnExtensionlessImageFilename_Succeeds()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var request = Request(name: "恆逸", appKey: "uuu");
        request.ImageFilename = "恆逸";

        var response = await client.PostAsJsonAsync("/api/partners", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Partner>(JsonOptions);
        Assert.Equal("恆逸", created!.ImageFilename);
    }

    [Theory]
    [InlineData("", "Cisco", "Cisco選單", "Cisco")]
    [InlineData("Cisco", "", "Cisco選單", "Cisco")]
    [InlineData("Cisco", "Cisco", "", "Cisco")]
    [InlineData("Cisco", "Cisco", "Cisco選單", "")]
    public async Task Create_WithABlankRequiredField_Returns400(
        string name, string appKey, string menuName, string detailName)
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/partners", new PartnerRequest
        {
            Name = name,
            AppKey = appKey,
            NameOnPartnerMenu = menuName,
            NameOnCourseDetailPage = detailName,
            DisplayOrder = 9999
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAnOverlongAppKey_Returns400()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        // varchar(10), and the longest live value is exactly 10 characters.
        var response = await client.PostAsJsonAsync(
            "/api/partners", Request(name: "Cisco", appKey: new string('x', 11)), JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAnOverlongName_Returns400()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/partners", Request(name: new string('x', 51), appKey: "Cisco"), JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10000)]
    public async Task Create_WithDisplayOrderOutOfRange_Returns400(int displayOrder)
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var request = Request(name: "Cisco", appKey: "Cisco");
        request.DisplayOrder = displayOrder;

        var response = await client.PostAsJsonAsync("/api/partners", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ChangesEveryWritableField_AndReturnsTheUpdatedRow()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/partners", new PartnerRequest
        {
            Pkid = 3,
            Name = "溫室氣體國際標準課程",
            AppKey = "PCB",
            NameOnPartnerMenu = "溫室氣體：ISO 14060系列課程",
            NameOnCourseDetailPage = "溫室氣體國際標準課程",
            DisplayOrder = 1,
            ImageFilename = "PCB.svg"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Partner>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal((short)3, updated.Pkid);
        Assert.Equal("溫室氣體國際標準課程", updated.Name);
        Assert.Equal("溫室氣體：ISO 14060系列課程", updated.NameOnPartnerMenu);
        Assert.Equal(1, updated.DisplayOrder);
        Assert.Equal("PCB.svg", updated.ImageFilename);
    }

    /// <summary>A row keeping its own AppKey must not 409 against itself.</summary>
    [Fact]
    public async Task Update_KeepingItsOwnAppKey_Returns200()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var request = Request(name: "改名了", appKey: "ISO");
        request.Pkid = 2;

        var response = await client.PutAsJsonAsync("/api/partners", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_TakingAnotherRowsAppKey_Returns409()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var request = Request(name: "國際標準課程", appKey: "ISO");
        request.Pkid = 3;

        var response = await client.PutAsJsonAsync("/api/partners", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("應用代碼重複", problem?.Title);
    }

    /// <summary>The two seeded duplicates must stay editable — a Name uniqueness check would 409 them.</summary>
    [Fact]
    public async Task Update_ToANameAnotherRowUses_Returns200NotConflict()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var request = Request(name: "國際標準課程", appKey: "Aruba");
        request.Pkid = 4;

        var response = await client.PutAsJsonAsync("/api/partners", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutPkid_Returns400AndDoesNotTouchRepository()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var request = Request(name: "無主代碼", appKey: "None");
        request.Pkid = 0;

        var response = await client.PutAsJsonAsync("/api/partners", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCallCount);
    }

    [Fact]
    public async Task Update_UnknownPkid_Returns404()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var request = Request(name: "幽靈", appKey: "Ghost");
        request.Pkid = 999;

        var response = await client.PutAsJsonAsync("/api/partners", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithBlankName_Returns400()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var request = Request(name: string.Empty, appKey: "Aruba");
        request.Pkid = 4;

        var response = await client.PutAsJsonAsync("/api/partners", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_UnusedPartner_Returns204_ThenGetReturns404()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/partners/3")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/partners/3")).StatusCode);
        Assert.Equal(3, (await ReadPartnersAsync(await client.GetAsync("/api/partners"))).Count);
    }

    [Fact]
    public async Task Delete_PartnerUsedByCourse_Returns409AndKeepsTheRow()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/partners/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("廠商使用中", problem?.Title);

        Assert.Equal(4, (await ReadPartnersAsync(await client.GetAsync("/api/partners"))).Count);
    }

    /// <summary>
    /// The case SQL Server would NOT catch: Seminar declares no foreign key, so the database
    /// would allow this delete and orphan 34 rows. The guard has to carry it.
    /// </summary>
    [Fact]
    public async Task Delete_PartnerReferencedOnlyBySeminar_Returns409AndKeepsTheRow()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/partners/2");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("廠商使用中", problem?.Title);
        Assert.Contains("34 筆研討會", problem?.Detail);

        Assert.Equal(4, (await ReadPartnersAsync(await client.GetAsync("/api/partners"))).Count);
    }

    [Fact]
    public async Task Delete_UnknownPkid_Returns404()
    {
        using var factory = new PartnerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/partners/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Helpers ----------

    private static PartnerRequest Request(string name, string appKey) => new()
    {
        Name = name,
        AppKey = appKey,
        NameOnPartnerMenu = string.IsNullOrEmpty(name) ? "選單" : name + "選單",
        NameOnCourseDetailPage = string.IsNullOrEmpty(name) ? "明細" : name,
        DisplayOrder = 9999
    };

    private static async Task<List<Partner>> QueryAsync(HttpClient client, PartnerQuery query)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(query, JsonOptions), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/partners/query", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadPartnersAsync(response);
    }

    private static async Task<List<Partner>> ReadPartnersAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<List<Partner>>(JsonOptions) ?? [];

    private sealed class ProblemDetailsDto
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
}
