using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CMS.API.Models;
using Xunit;

namespace CMS.API.Tests;

public class FeaturedPromoItemsControllerTests
{
    private const string BaseUrl = "/api/featured-promo-items";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ---------- Week filter ----------

    /// <summary>
    /// weekOf is a Wednesday; the API must widen it to Monday 03-16 .. Sunday 03-22 and drop the
    /// rows on 03-15 (previous Sunday) and 03-23 (next Monday).
    /// </summary>
    [Fact]
    public async Task Query_WeekOfAWednesday_ReturnsMondayThroughSundayOnly()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var items = await QueryAsync(client, new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 1,
            WeekOf = new DateOnly(2026, 3, 18)
        });

        Assert.Equal([1, 2, 3, 4, 5], items.Select(i => i.Pkid));
        Assert.DoesNotContain(items, i => i.Pkid is 6 or 7);
    }

    /// <summary>
    /// Sunday is DayOfWeek 0. A naive "subtract DayOfWeek − 1 days" would jump to the NEXT
    /// Monday, so a Sunday weekOf has to resolve back to the Monday six days earlier.
    /// </summary>
    [Fact]
    public async Task Query_WeekOfASunday_ResolvesToThePrecedingMonday()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var items = await QueryAsync(client, new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 1,
            WeekOf = new DateOnly(2026, 3, 22)
        });

        Assert.Equal([1, 2, 3, 4, 5], items.Select(i => i.Pkid));
    }

    [Fact]
    public async Task Query_WeekOfTheMondayItself_ReturnsTheSameWeek()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var items = await QueryAsync(client, new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 1,
            WeekOf = new DateOnly(2026, 3, 16)
        });

        Assert.Equal([1, 2, 3, 4, 5], items.Select(i => i.Pkid));
    }

    [Fact]
    public async Task Query_WeekOfTheNextMonday_ReturnsOnlyNextWeeksRow()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var items = await QueryAsync(client, new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 1,
            WeekOf = new DateOnly(2026, 3, 23)
        });

        Assert.Equal(6, Assert.Single(items).Pkid);
    }

    [Fact]
    public void StartOfWeek_MapsEveryDayOfTheWeekToItsMonday()
    {
        var monday = new DateOnly(2026, 3, 16);

        for (var offset = 0; offset < 7; offset++)
        {
            Assert.Equal(monday, FeaturedPromoItemQuery.StartOfWeek(monday.AddDays(offset)));
        }

        Assert.Equal(new DateOnly(2026, 3, 9), FeaturedPromoItemQuery.StartOfWeek(new DateOnly(2026, 3, 15)));
    }

    // ---------- TrainingCenter filter ----------

    [Fact]
    public async Task Query_ByTrainingCenter_ExcludesOtherCentres()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var items = await QueryAsync(client, new FeaturedPromoItemQuery { TrainingCenterPkid = 2 });

        var only = Assert.Single(items);
        Assert.Equal(8, only.Pkid);
        Assert.Equal("新竹", only.TrainingCenterName);
    }

    [Fact]
    public async Task Query_ByCentreAndWeek_CombinesBothFilters()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var items = await QueryAsync(client, new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 2,
            WeekOf = new DateOnly(2026, 3, 20)
        });

        Assert.Equal(8, Assert.Single(items).Pkid);

        var nextWeek = await QueryAsync(client, new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 2,
            WeekOf = new DateOnly(2026, 3, 27)
        });
        Assert.Empty(nextWeek);
    }

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsEveryRowInDateCentreSlotOrder()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var items = await QueryAsync(client, new FeaturedPromoItemQuery());

        // 03-15, then the three 台北 slots on 03-16, then 新竹 on 03-16, then 03-17, 03-22, 03-23.
        Assert.Equal([7, 1, 2, 3, 8, 4, 5, 6], items.Select(i => i.Pkid));
    }

    [Fact]
    public async Task Query_WithNoMatch_ReturnsEmptyList()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var items = await QueryAsync(client, new FeaturedPromoItemQuery { WeekOf = new DateOnly(2027, 1, 1) });

        Assert.Empty(items);
    }

    // ---------- List / View ----------

    [Fact]
    public async Task GetAll_CarriesTheJoinedLabelsOnEveryRow()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await ReadItemsAsync(response);
        Assert.Equal(8, items.Count);

        var first = items.Single(i => i.Pkid == 1);
        Assert.Equal("台北", first.TrainingCenterName);
        Assert.Equal("20251204_SkillTrainAI", first.PromoCode);
        Assert.Equal(3403, first.PromotionPkid);
    }

    /// <summary>ScheduleOn is a `date` column — it must travel as yyyy-MM-dd with no time part.</summary>
    [Fact]
    public async Task GetById_SerializesScheduleOnAsADateOnlyString()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync($"{BaseUrl}/5");

        using var document = JsonDocument.Parse(json);
        Assert.Equal("2026-03-22", document.RootElement.GetProperty("scheduleOn").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("slot").GetInt32());
    }

    [Fact]
    public async Task GetById_ReturnsTheItemWithEveryField()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}/2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);
        Assert.NotNull(item);
        Assert.Equal(2, item.Pkid);
        Assert.Equal(new DateOnly(2026, 3, 16), item.ScheduleOn);
        Assert.Equal((short)1, item.TrainingCenterPkid);
        Assert.Equal((byte)2, item.Slot);
        Assert.Equal("20251215_n8n", item.PromoCode);
        Assert.Equal("n8n自動化三部曲", item.Topic);
        Assert.Equal("從自動化新手到企業級AI架構師學習路徑", item.Description);
    }

    [Fact]
    public async Task GetById_UnknownPkid_Returns404()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_AssignsAnIdentityPkid_AndReturnsCreated()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, Request("2026-03-17", slot: 2), JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(9, created.Pkid);
        Assert.Equal((byte)2, created.Slot);
        Assert.Equal("20251215_n8n", created.PromoCode);
        Assert.Equal($"{BaseUrl}/{created.Pkid}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidSuppliedInTheBody()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-18", slot: 1);
        request.Pkid = 99;

        var created = await (await client.PostAsJsonAsync(BaseUrl, request, JsonOptions))
            .Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);

        Assert.Equal(9, created!.Pkid);
    }

    [Fact]
    public async Task Create_TrimsTopicAndDescription()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-18", slot: 1);
        request.Topic = "  留白的主題  ";
        request.Description = "  留白的說明  ";

        var created = await (await client.PostAsJsonAsync(BaseUrl, request, JsonOptions))
            .Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);

        Assert.Equal("留白的主題", created!.Topic);
        Assert.Equal("留白的說明", created.Description);
    }

    /// <summary>The unique index IX_FeaturedPromoItem_UniqueDateLocSlot would 500 this — it must 409 instead.</summary>
    [Fact]
    public async Task Create_IntoAnOccupiedSlot_Returns409()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, Request("2026-03-16", slot: 2), JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("時段重複", problem?.Title);
        Assert.Contains("2026-03-16", problem?.Detail);

        Assert.Equal(8, (await ReadItemsAsync(await client.GetAsync(BaseUrl))).Count);
    }

    /// <summary>The same slot at another centre is a different key — no conflict.</summary>
    [Fact]
    public async Task Create_SameSlotAtAnotherCentre_Returns201()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-16", slot: 2);
        request.TrainingCenterPkid = 2;

        var response = await client.PostAsJsonAsync(BaseUrl, request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Create_WithSlotOutsideOneToThree_Returns400(byte slot)
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, Request("2026-03-18", slot), JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutScheduleOn_Returns400()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(BaseUrl, new StringContent(
            """{"trainingCenterPkid":1,"slot":1,"promotionPkid":3403,"topic":"t","description":"d"}""",
            Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", "說明")]
    [InlineData("主題", "")]
    public async Task Create_WithABlankRequiredText_Returns400(string topic, string description)
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-18", slot: 1);
        request.Topic = topic;
        request.Description = description;

        var response = await client.PostAsJsonAsync(BaseUrl, request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAPromotion_Returns400()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-18", slot: 1);
        request.PromotionPkid = 0;

        var response = await client.PostAsJsonAsync(BaseUrl, request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAnOverlongTopic_Returns400()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-18", slot: 1);
        request.Topic = new string('x', 101);

        var response = await client.PostAsJsonAsync(BaseUrl, request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ChangesEveryWritableField_AndReturnsTheUpdatedRow()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-19", slot: 3);
        request.Pkid = 4;
        request.PromotionPkid = 3393;
        request.Topic = "改過的主題";
        request.Description = "改過的說明";

        var response = await client.PutAsJsonAsync(BaseUrl, request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(4, updated.Pkid);
        Assert.Equal(new DateOnly(2026, 3, 19), updated.ScheduleOn);
        Assert.Equal((byte)3, updated.Slot);
        Assert.Equal("20251219_GoogleAIseminar", updated.PromoCode);
        Assert.Equal("改過的主題", updated.Topic);
        Assert.Equal("改過的說明", updated.Description);
    }

    /// <summary>A row keeping its own (date, centre, slot) must not 409 against itself.</summary>
    [Fact]
    public async Task Update_KeepingItsOwnSlot_Returns200()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-16", slot: 2);
        request.Pkid = 2;
        request.Topic = "只改主題";

        var response = await client.PutAsJsonAsync(BaseUrl, request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_MovingOntoAnotherRowsSlot_Returns409()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-16", slot: 1);
        request.Pkid = 2;

        var response = await client.PutAsJsonAsync(BaseUrl, request, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("時段重複", problem?.Title);
        Assert.Equal(0, factory.Repository.UpdateCallCount);
    }

    [Fact]
    public async Task Update_WithoutPkid_Returns400AndDoesNotTouchRepository()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-18", slot: 1);
        request.Pkid = 0;

        var response = await client.PutAsJsonAsync(BaseUrl, request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCallCount);
    }

    [Fact]
    public async Task Update_UnknownPkid_Returns404()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var request = Request("2026-03-18", slot: 1);
        request.Pkid = 999;

        var response = await client.PutAsJsonAsync(BaseUrl, request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_Returns204_ThenGetReturns404()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{BaseUrl}/3")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{BaseUrl}/3")).StatusCode);
        Assert.Equal(7, (await ReadItemsAsync(await client.GetAsync(BaseUrl))).Count);
    }

    [Fact]
    public async Task Delete_UnknownPkid_Returns404()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"{BaseUrl}/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Move (the +/− buttons) ----------

    /// <summary>「+」 on slot 1 swaps it with slot 2 — both rows change, nothing else does.</summary>
    [Fact]
    public async Task MoveDown_IntoAnOccupiedSlot_SwapsTheTwoRows()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"{BaseUrl}/1/move-down", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = await response.Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);
        Assert.Equal(1, moved!.Pkid);
        Assert.Equal((byte)2, moved.Slot);

        var day = await QueryAsync(client, new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 1,
            WeekOf = new DateOnly(2026, 3, 16)
        });
        var monday = day.Where(i => i.ScheduleOn == new DateOnly(2026, 3, 16)).ToList();
        Assert.Equal([2, 1, 3], monday.Select(i => i.Pkid));
        Assert.Equal([(byte)1, (byte)2, (byte)3], monday.Select(i => i.Slot));
    }

    /// <summary>「−」 on slot 2 is the mirror image.</summary>
    [Fact]
    public async Task MoveUp_IntoAnOccupiedSlot_SwapsTheTwoRows()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"{BaseUrl}/3/move-up", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = await response.Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);
        Assert.Equal((byte)2, moved!.Slot);

        var two = await (await client.GetAsync($"{BaseUrl}/2")).Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);
        Assert.Equal((byte)3, two!.Slot);
    }

    /// <summary>pkid 4 is alone on its day, so moving it down just renumbers it.</summary>
    [Fact]
    public async Task MoveDown_IntoAnEmptySlot_JustRenumbersTheRow()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"{BaseUrl}/4/move-down", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = await response.Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);
        Assert.Equal((byte)2, moved!.Slot);

        var tuesday = (await QueryAsync(client, new FeaturedPromoItemQuery { TrainingCenterPkid = 1, WeekOf = new DateOnly(2026, 3, 17) }))
            .Where(i => i.ScheduleOn == new DateOnly(2026, 3, 17))
            .ToList();
        Assert.Equal(4, Assert.Single(tuesday).Pkid);
    }

    [Fact]
    public async Task MoveUp_FromTheFirstSlot_Returns409AndChangesNothing()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"{BaseUrl}/1/move-up", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("無法移動", problem?.Title);

        var one = await (await client.GetAsync($"{BaseUrl}/1")).Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);
        Assert.Equal((byte)1, one!.Slot);
    }

    [Fact]
    public async Task MoveDown_FromTheLastSlot_Returns409AndChangesNothing()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"{BaseUrl}/3/move-down", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var three = await (await client.GetAsync($"{BaseUrl}/3")).Content.ReadFromJsonAsync<FeaturedPromoItem>(JsonOptions);
        Assert.Equal((byte)3, three!.Slot);
    }

    [Fact]
    public async Task Move_UnknownPkid_Returns404()
    {
        using var factory = new FeaturedPromoItemApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"{BaseUrl}/999/move-up", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"{BaseUrl}/999/move-down", null)).StatusCode);
    }

    // ---------- Helpers ----------

    private static FeaturedPromoItemRequest Request(string scheduleOn, byte slot) => new()
    {
        ScheduleOn = DateOnly.Parse(scheduleOn),
        TrainingCenterPkid = 1,
        Slot = slot,
        PromotionPkid = 3423,
        Topic = "n8n自動化三部曲",
        Description = "從自動化新手到企業級AI架構師學習路徑"
    };

    private static async Task<List<FeaturedPromoItem>> QueryAsync(HttpClient client, FeaturedPromoItemQuery query)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(query, JsonOptions), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{BaseUrl}/query", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadItemsAsync(response);
    }

    private static async Task<List<FeaturedPromoItem>> ReadItemsAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<List<FeaturedPromoItem>>(JsonOptions) ?? [];

    private sealed class ProblemDetailsDto
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
}
