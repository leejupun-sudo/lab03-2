using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CMS.API.Models;
using Xunit;

namespace CMS.API.Tests;

public class PublishStatusesControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ---------- List / filter ----------

    [Fact]
    public async Task GetAll_ReturnsAllStatuses_OrderedByPkid()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/publish-statuses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statuses = await ReadStatusesAsync(response);
        Assert.Equal([(byte)1, (byte)2, (byte)3], statuses.Select(s => s.Pkid));
        Assert.Equal(["草稿", "上架中", "已下架"], statuses.Select(s => s.Description));
    }

    [Fact]
    public async Task GetAll_IncludesCourseAndPromotionUsageCounts()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var statuses = await ReadStatusesAsync(await client.GetAsync("/api/publish-statuses"));

        var published = statuses.Single(s => s.Pkid == 2);
        Assert.Equal(12, published.CourseCount);
        Assert.Equal(3, published.Promotion2Count);

        var draft = statuses.Single(s => s.Pkid == 1);
        Assert.Equal(0, draft.CourseCount);
        Assert.Equal(0, draft.Promotion2Count);
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesDescription()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var statuses = await QueryAsync(client, new PublishStatusQuery { Keyword = "下架" });

        Assert.Equal((byte)3, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_WithIsPublishedTrue_ReturnsOnlyPublished()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var statuses = await QueryAsync(client, new PublishStatusQuery { IsPublished = true });

        Assert.Equal((byte)2, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_WithBoolFalse_MatchesRowsWhereTheFlagIsOff()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var notDraft = await QueryAsync(client, new PublishStatusQuery { IsDraft = false });

        Assert.Equal([(byte)2, (byte)3], notDraft.Select(s => s.Pkid));
    }

    [Fact]
    public async Task Query_CombinesFilters()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var statuses = await QueryAsync(client, new PublishStatusQuery
        {
            IsDraft = false,
            IsDiscontinued = true
        });

        Assert.Equal((byte)3, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllStatuses()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var statuses = await QueryAsync(client, new PublishStatusQuery());

        Assert.Equal(3, statuses.Count);
    }

    [Fact]
    public async Task Query_WithNoMatch_ReturnsEmptyList()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var statuses = await QueryAsync(client, new PublishStatusQuery { Keyword = "no-such-status" });

        Assert.Empty(statuses);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsTheStatusWithItsFlagsAndCounts()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/publish-statuses/2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<PublishStatus>(JsonOptions);
        Assert.NotNull(status);
        Assert.Equal((byte)2, status.Pkid);
        Assert.Equal("上架中", status.Description);
        Assert.False(status.IsDraft);
        Assert.True(status.IsPublished);
        Assert.False(status.IsDiscontinued);
        Assert.Equal(12, status.CourseCount);
        Assert.Equal(3, status.Promotion2Count);
    }

    [Fact]
    public async Task GetById_UnknownPkid_Returns404()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/publish-statuses/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AboveTinyintRange_Returns400()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        // The route constraint is :int but the action takes a byte — 300 fails model binding.
        var response = await client.GetAsync("/api/publish-statuses/300");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_PersistsTheClientSuppliedPkid_AndReturnsCreated()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/publish-statuses", new PublishStatusRequest
        {
            Pkid = 4,
            Description = "審核中",
            IsDraft = true,
            IsPublished = false,
            IsDiscontinued = false
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PublishStatus>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal((byte)4, created.Pkid);
        Assert.Equal("審核中", created.Description);
        Assert.True(created.IsDraft);
        Assert.Equal("/api/publish-statuses/4", response.Headers.Location?.AbsolutePath);

        var fetched = await client.GetFromJsonAsync<PublishStatus>("/api/publish-statuses/4", JsonOptions);
        Assert.Equal("審核中", fetched!.Description);
    }

    [Fact]
    public async Task Create_WithDuplicatePkid_Returns409()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/publish-statuses", new PublishStatusRequest
        {
            Pkid = 1,
            Description = "重複"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(3, (await ReadStatusesAsync(await client.GetAsync("/api/publish-statuses"))).Count);
    }

    [Fact]
    public async Task Create_WithBlankDescription_Returns400()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/publish-statuses", new PublishStatusRequest
        {
            Pkid = 4,
            Description = string.Empty
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithZeroPkid_Returns400()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        // 0 is reserved as the "not supplied" sentinel — [Range(1, 255)] rejects it.
        var response = await client.PostAsJsonAsync("/api/publish-statuses", new PublishStatusRequest
        {
            Pkid = 0,
            Description = "無主代碼"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ChangesDescriptionAndFlags_AndReturnsTheUpdatedRow()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/publish-statuses", new PublishStatusRequest
        {
            Pkid = 3,
            Description = "已封存",
            IsDraft = false,
            IsPublished = false,
            IsDiscontinued = true
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<PublishStatus>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal((byte)3, updated.Pkid);
        Assert.Equal("已封存", updated.Description);
        Assert.True(updated.IsDiscontinued);
    }

    [Fact]
    public async Task Update_LeavesTheRestOfTheTableAlone()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        await client.PutAsJsonAsync("/api/publish-statuses", new PublishStatusRequest
        {
            Pkid = 1,
            Description = "草稿 (改)",
            IsDraft = true
        }, JsonOptions);

        var statuses = await ReadStatusesAsync(await client.GetAsync("/api/publish-statuses"));
        Assert.Equal([(byte)1, (byte)2, (byte)3], statuses.Select(s => s.Pkid));
        Assert.Equal("上架中", statuses.Single(s => s.Pkid == 2).Description);
    }

    [Fact]
    public async Task Update_WithoutPkid_Returns400AndDoesNotTouchRepository()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/publish-statuses", new PublishStatusRequest
        {
            Pkid = 0,
            Description = "草稿"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCallCount);
    }

    [Fact]
    public async Task Update_UnknownPkid_Returns404()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/publish-statuses", new PublishStatusRequest
        {
            Pkid = 99,
            Description = "幽靈"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_UnusedStatus_Returns204_ThenGetReturns404()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/publish-statuses/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/publish-statuses/1")).StatusCode);
        Assert.Equal(2, (await ReadStatusesAsync(await client.GetAsync("/api/publish-statuses"))).Count);
    }

    [Fact]
    public async Task Delete_StatusUsedByCourseOrPromotion_Returns409AndKeepsTheRow()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/publish-statuses/2");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("狀態使用中", problem?.Title);

        Assert.Equal(3, (await ReadStatusesAsync(await client.GetAsync("/api/publish-statuses"))).Count);
    }

    [Fact]
    public async Task Delete_UnknownPkid_Returns404()
    {
        using var factory = new PublishStatusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/publish-statuses/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Helpers ----------

    private static async Task<List<PublishStatus>> QueryAsync(HttpClient client, PublishStatusQuery query)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(query, JsonOptions), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/publish-statuses/query", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadStatusesAsync(response);
    }

    private static async Task<List<PublishStatus>> ReadStatusesAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<List<PublishStatus>>(JsonOptions) ?? [];

    private sealed class ProblemDetailsDto
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
}
