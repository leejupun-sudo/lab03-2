using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CMS.API.Models;
using Xunit;

namespace CMS.API.Tests;

public class CourseGroupsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ---------- List / filter ----------

    [Fact]
    public async Task GetAll_ReturnsAllGroups_OrderedByDescription()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/course-groups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await ReadGroupsAsync(response);
        Assert.Equal(
            ["Azure系列課程", "Azure系列課程", "SharePoint系列課程"],
            groups.Select(g => g.Description));
    }

    [Fact]
    public async Task GetAll_IncludesCourseAndPartnerCourseGroupUsageCounts()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var groups = await ReadGroupsAsync(await client.GetAsync("/api/course-groups"));

        var used = groups.Single(g => g.Pkid == 1);
        Assert.Equal(48, used.CourseCount);
        Assert.Equal(2, used.PartnerCourseGroupCount);

        var unused = groups.Single(g => g.Pkid == 2);
        Assert.Equal(0, unused.CourseCount);
        Assert.Equal(0, unused.PartnerCourseGroupCount);
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesDescription()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var groups = await QueryAsync(client, new CourseGroupQuery { Keyword = "SharePoint" });

        Assert.Equal((short)2, Assert.Single(groups).Pkid);
    }

    [Fact]
    public async Task Query_WithKeywordMatchingDuplicates_ReturnsBothRows()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var groups = await QueryAsync(client, new CourseGroupQuery { Keyword = "Azure" });

        Assert.Equal([(short)1, (short)3], groups.Select(g => g.Pkid).Order());
    }

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllGroups()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var groups = await QueryAsync(client, new CourseGroupQuery());

        Assert.Equal(3, groups.Count);
    }

    [Fact]
    public async Task Query_WithNoMatch_ReturnsEmptyList()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var groups = await QueryAsync(client, new CourseGroupQuery { Keyword = "no-such-group" });

        Assert.Empty(groups);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsTheGroupWithItsCounts()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/course-groups/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<CourseGroup>(JsonOptions);
        Assert.NotNull(group);
        Assert.Equal((short)1, group.Pkid);
        Assert.Equal("Azure系列課程", group.Description);
        Assert.Equal(48, group.CourseCount);
        Assert.Equal(2, group.PartnerCourseGroupCount);
    }

    [Fact]
    public async Task GetById_UnknownPkid_Returns404()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/course-groups/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AboveSmallintRange_Returns400()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        // The route constraint is :int but the action takes a short — 40000 fails model binding.
        var response = await client.GetAsync("/api/course-groups/40000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_AssignsAnIdentityPkid_AndReturnsCreated()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Description = "Kubernetes系列課程"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CourseGroup>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal((short)4, created.Pkid);
        Assert.Equal("Kubernetes系列課程", created.Description);
        Assert.Equal($"/api/course-groups/{created.Pkid}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidSuppliedInTheBody()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Pkid = 99,
            Description = "IDENTITY 測試"
        }, JsonOptions);

        var created = await response.Content.ReadFromJsonAsync<CourseGroup>(JsonOptions);
        Assert.Equal((short)4, created!.Pkid);
    }

    /// <summary>
    /// Description carries no UNIQUE constraint and the live table holds duplicates,
    /// so a repeat name must succeed rather than 409.
    /// </summary>
    [Fact]
    public async Task Create_WithADescriptionThatAlreadyExists_Returns201NotConflict()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Description = "Azure系列課程"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var groups = await ReadGroupsAsync(await client.GetAsync("/api/course-groups"));
        Assert.Equal(3, groups.Count(g => g.Description == "Azure系列課程"));
    }

    [Fact]
    public async Task Create_WithBlankDescription_Returns400()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Description = string.Empty
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithOverlongDescription_Returns400()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Description = new string('x', 101)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ChangesTheDescription_AndReturnsTheUpdatedRow()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Pkid = 2,
            Description = "SharePoint進階系列"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CourseGroup>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal((short)2, updated.Pkid);
        Assert.Equal("SharePoint進階系列", updated.Description);
    }

    /// <summary>The two seeded duplicates must stay editable — a uniqueness check would 409 them.</summary>
    [Fact]
    public async Task Update_ToADescriptionAnotherRowUses_Returns200NotConflict()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Pkid = 2,
            Description = "Azure系列課程"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_ADuplicateRowKeepingItsOwnName_Succeeds()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Pkid = 3,
            Description = "Azure系列課程"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutPkid_Returns400AndDoesNotTouchRepository()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Pkid = 0,
            Description = "無主代碼"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCallCount);
    }

    [Fact]
    public async Task Update_UnknownPkid_Returns404()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Pkid = 999,
            Description = "幽靈"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithBlankDescription_Returns400()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/course-groups", new CourseGroupRequest
        {
            Pkid = 2,
            Description = string.Empty
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_UnusedGroup_Returns204_ThenGetReturns404()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/course-groups/2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/course-groups/2")).StatusCode);
        Assert.Equal(2, (await ReadGroupsAsync(await client.GetAsync("/api/course-groups"))).Count);
    }

    [Fact]
    public async Task Delete_GroupUsedByCourseOrPartnerCourseGroup_Returns409AndKeepsTheRow()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/course-groups/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("群組使用中", problem?.Title);

        Assert.Equal(3, (await ReadGroupsAsync(await client.GetAsync("/api/course-groups"))).Count);
    }

    [Fact]
    public async Task Delete_UnknownPkid_Returns404()
    {
        using var factory = new CourseGroupApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/course-groups/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Helpers ----------

    private static async Task<List<CourseGroup>> QueryAsync(HttpClient client, CourseGroupQuery query)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(query, JsonOptions), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/course-groups/query", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadGroupsAsync(response);
    }

    private static async Task<List<CourseGroup>> ReadGroupsAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<List<CourseGroup>>(JsonOptions) ?? [];

    private sealed class ProblemDetailsDto
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
}
