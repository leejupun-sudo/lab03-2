using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CMS.API.Models;
using Xunit;

namespace CMS.API.Tests;

public class LookupsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetAppRoles_ReturnsRowsOrderedByRoleId()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookups/app-roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = await response.Content.ReadFromJsonAsync<List<AppRoleLookup>>(JsonOptions);
        Assert.NotNull(roles);
        Assert.Equal(["Admin", "User"], roles.Select(r => r.RoleId));
        Assert.Equal([1, 2], roles.Select(r => r.Pkid));
    }

    /// <summary>Label is computed — read the raw JSON so the `RoleName (RoleId)` shape is proven on the wire.</summary>
    [Fact]
    public async Task GetAppRoles_SerializesLabelOnTheWire()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/lookups/app-roles");

        using var document = JsonDocument.Parse(json);
        var labels = document.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("label").GetString())
            .ToList();

        Assert.Equal(["Administrator (Admin)", "User (User)"], labels);
    }

    [Fact]
    public async Task GetPublishStatuses_ReturnsRowsOrderedByPkid()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookups/publish-statuses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statuses = await response.Content.ReadFromJsonAsync<List<PublishStatusLookup>>(JsonOptions);
        Assert.NotNull(statuses);
        Assert.Equal([(byte)1, (byte)2, (byte)3], statuses.Select(s => s.Pkid));
        Assert.Equal(["草稿", "上架中", "已下架"], statuses.Select(s => s.Description));
    }

    /// <summary>
    /// `Label` is a computed C# property, so a round-trip through the DTO would assert nothing.
    /// Read the raw JSON to prove the label actually reaches the Angular `optionLabel="label"` binding.
    /// </summary>
    [Fact]
    public async Task GetPublishStatuses_SerializesLabelOnTheWire()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/lookups/publish-statuses");

        using var document = JsonDocument.Parse(json);
        var labels = document.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("label").GetString())
            .ToList();

        Assert.Equal(["草稿", "上架中", "已下架"], labels);
    }

    [Fact]
    public async Task GetCourseGroups_ReturnsRowsOrderedByDescription()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookups/course-groups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<List<CourseGroupLookup>>(JsonOptions);
        Assert.NotNull(groups);
        Assert.Equal(
            ["Azure系列課程", "PMI®專案管理認證系列", "SharePoint系列課程"],
            groups.Select(g => g.Description));
        Assert.Equal([(short)3, (short)5, (short)7], groups.Select(g => g.Pkid));
    }

    [Fact]
    public async Task GetCourseGroups_SerializesLabelOnTheWire()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/lookups/course-groups");

        using var document = JsonDocument.Parse(json);
        var labels = document.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("label").GetString())
            .ToList();

        Assert.Equal(
            ["Azure系列課程", "PMI®專案管理認證系列", "SharePoint系列課程"],
            labels);
    }

    [Fact]
    public async Task GetPartners_ReturnsRowsOrderedByDisplayOrderThenName()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookups/partners");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var partners = await response.Content.ReadFromJsonAsync<List<PartnerLookup>>(JsonOptions);
        Assert.NotNull(partners);
        Assert.Equal([(short)11, (short)19, (short)31], partners.Select(p => p.Pkid));
    }

    /// <summary>
    /// Two of the three seeded partners share a Name, so a bare-Name label would render
    /// indistinguishable options. The composed label is what makes the dropdown usable.
    /// </summary>
    [Fact]
    public async Task GetPartners_SerializesTheComposedLabelOnTheWire()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/lookups/partners");

        using var document = JsonDocument.Parse(json);
        var labels = document.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("label").GetString())
            .ToList();

        Assert.Equal(
            ["CompTIA (CompTIA)", "國際標準課程 (ISO)", "國際標準課程 (PCB)"],
            labels);
    }

    [Fact]
    public async Task GetAppUsers_SerializesTheComposedLabelOnTheWire()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/lookups/app-users");

        using var document = JsonDocument.Parse(json);
        var labels = document.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("label").GetString())
            .ToList();

        Assert.Contains("Miles Sun (miles@uuu.com.tw)", labels);
    }

    [Fact]
    public async Task GetCertifications_ReturnsRowsOrderedByPartnerThenTitle_WithTrimmedTitles()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookups/certifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var certifications = await response.Content.ReadFromJsonAsync<List<CertificationLookup>>(JsonOptions);
        Assert.NotNull(certifications);
        Assert.Equal([5, 36, 34], certifications.Select(c => c.Pkid));
        // nchar(100) padding must not survive the SELECT.
        Assert.Equal(["CCNA", "FCP-PCS", "FCP-SN"], certifications.Select(c => c.Title));
    }

    /// <summary>The label composes Title and partner name; read the raw JSON to prove it reaches the wire.</summary>
    [Fact]
    public async Task GetCertifications_SerializesTheComposedLabelOnTheWire()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/lookups/certifications");

        using var document = JsonDocument.Parse(json);
        var labels = document.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("label").GetString())
            .ToList();

        Assert.Equal(
            ["CCNA (Cisco)", "FCP-PCS (Fortinet資安專家認證課程)", "FCP-SN (Fortinet資安專家認證課程)"],
            labels);
    }

    [Fact]
    public async Task GetJobCategories_ReturnsRowsOrderedByPkid()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookups/job-categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<JobCategoryLookup>>(JsonOptions);
        Assert.NotNull(categories);
        Assert.Equal([(short)1, (short)16, (short)24], categories.Select(c => c.Pkid));
    }

    [Fact]
    public async Task GetJobCategories_SerializesLabelOnTheWire()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/lookups/job-categories");

        using var document = JsonDocument.Parse(json);
        var labels = document.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("label").GetString())
            .ToList();

        Assert.Equal(
            ["網路系統工程 System Engineer", "雲端技術 Cloud - Microsoft Azure", "資訊安全 Security"],
            labels);
    }

    [Fact]
    public async Task GetCourses_ReturnsRowsOrderedByCourseId_WithComposedLabel()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/lookups/courses");

        using var document = JsonDocument.Parse(json);
        var rows = document.RootElement.EnumerateArray()
            .Select(element => (element.GetProperty("pkid").GetInt32(), element.GetProperty("label").GetString()))
            .ToList();

        Assert.Equal(
            [(2063, "14064GLV ISO 14064溫室氣體主導查證師／確證師訓練課程"), (41, "IINS CCNA Security認證-建置Cisco網路安全"), (35, "PLF Oracle資料庫之PL／SQL基礎")],
            rows);
    }

    // ---------- training-centers (the FeaturedPromoItem tabs) ----------

    [Fact]
    public async Task GetTrainingCenters_ReturnsRowsOrderedByDisplayOrder()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookups/training-centers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var centres = await response.Content.ReadFromJsonAsync<List<TrainingCenterLookup>>(JsonOptions);
        Assert.NotNull(centres);
        // pkid 54 (線上研討會) is last by DisplayOrder, not first by insertion or by pkid.
        Assert.Equal([(short)1, (short)2, (short)3, (short)5, (short)54], centres.Select(c => c.Pkid));
        Assert.Equal(["台北", "新竹", "台中", "高雄", "線上研討會"], centres.Select(c => c.Name));
    }

    /// <summary>Label is computed — read the raw JSON so the tab caption is proven on the wire.</summary>
    [Fact]
    public async Task GetTrainingCenters_SerializesLabelOnTheWire()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/lookups/training-centers");

        using var document = JsonDocument.Parse(json);
        var labels = document.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("label").GetString())
            .ToList();

        Assert.Equal(["台北", "新竹", "台中", "高雄", "線上研討會"], labels);
    }

    // ---------- promotion2s (the PromoCode autocomplete) ----------

    [Fact]
    public async Task GetPromotion2s_WithoutKeyword_ReturnsNewestScheduleOnFirst()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookups/promotion2s");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var promotions = await response.Content.ReadFromJsonAsync<List<Promotion2Lookup>>(JsonOptions);
        Assert.NotNull(promotions);
        // 2000 has the newest ScheduleOn despite the lowest pkid.
        Assert.Equal([2000, 3393, 3423, 3403], promotions.Select(p => p.Pkid));
    }

    [Fact]
    public async Task GetPromotion2s_WithKeyword_MatchesPromoCodeCaseInsensitively()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var promotions = await client.GetFromJsonAsync<List<Promotion2Lookup>>(
            "/api/lookups/promotion2s?keyword=n8n", JsonOptions);

        Assert.NotNull(promotions);
        Assert.Equal(["N8N-legacy", "20251215_n8n"], promotions.Select(p => p.PromoCode));
    }

    [Fact]
    public async Task GetPromotion2s_WithKeyword_TrimsAndMatchesAnywhereInTheCode()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var promotions = await client.GetFromJsonAsync<List<Promotion2Lookup>>(
            "/api/lookups/promotion2s?keyword=%20Google%20", JsonOptions);

        Assert.NotNull(promotions);
        Assert.Equal(3393, Assert.Single(promotions).Pkid);
    }

    [Fact]
    public async Task GetPromotion2s_WithNoMatch_ReturnsEmptyList()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var promotions = await client.GetFromJsonAsync<List<Promotion2Lookup>>(
            "/api/lookups/promotion2s?keyword=no-such-code", JsonOptions);

        Assert.NotNull(promotions);
        Assert.Empty(promotions);
    }

    /// <summary>
    /// The endpoint feeds an autocomplete, not a full dropdown: with 1157 live rows it is
    /// capped at 20. The cap is applied AFTER ordering, so the newest 20 survive.
    /// </summary>
    [Fact]
    public async Task GetPromotion2s_CapsTheResultAtTwentyNewest()
    {
        using var factory = new LookupApiFactory();
        for (var day = 1; day <= 30; day++)
        {
            factory.Repository.SeedPromotion2(9000 + day, $"20260601_bulk{day:00}", "bulk", "bulk", $"2026-06-{day:00}");
        }
        using var client = factory.CreateClient();

        var promotions = await client.GetFromJsonAsync<List<Promotion2Lookup>>(
            "/api/lookups/promotion2s?keyword=bulk", JsonOptions);

        Assert.NotNull(promotions);
        Assert.Equal(20, promotions.Count);
        Assert.Equal(9030, promotions[0].Pkid);
        Assert.Equal(9011, promotions[^1].Pkid);
    }

    /// <summary>
    /// The form pre-fills Topic / Description from the chosen promotion and binds the label to
    /// the autocomplete, so all three must reach the wire as-is.
    /// </summary>
    [Fact]
    public async Task GetPromotion2s_SerializesLabelTopicAndDescriptionOnTheWire()
    {
        using var factory = new LookupApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/lookups/promotion2s?keyword=SkillTrainAI");

        using var document = JsonDocument.Parse(json);
        var row = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("20251204_SkillTrainAI", row.GetProperty("label").GetString());
        Assert.Equal("成為能AI協作的程式設計師", row.GetProperty("topic").GetString());
        Assert.Equal("轉職就業養成班", row.GetProperty("description").GetString());
    }
}
