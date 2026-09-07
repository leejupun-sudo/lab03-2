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
}
