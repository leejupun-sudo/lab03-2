using CMS.API.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>
/// Hosts the real API pipeline with the SQL-backed repository swapped for
/// <see cref="FakeFeaturedPromoItemRepository"/>, so the endpoint tests need no database.
/// </summary>
public class FeaturedPromoItemApiFactory : WebApplicationFactory<Program>
{
    // The seed is built around the week of Monday 2026-03-16 .. Sunday 2026-03-22 for centre 1
    // (台北), with one row on each side of the boundary so the week filter is actually tested:
    //
    //   pkid 1  03-16 Mon  台北 slot 1   } a full day — the swap case for move-down / move-up
    //   pkid 2  03-16 Mon  台北 slot 2   }
    //   pkid 3  03-16 Mon  台北 slot 3   } — and the "already last" 409 case
    //   pkid 4  03-17 Tue  台北 slot 1     alone on its day — move-down into an EMPTY slot
    //   pkid 5  03-22 Sun  台北 slot 1     the last day IN the week (Sunday is DayOfWeek 0)
    //   pkid 6  03-23 Mon  台北 slot 1     next week — must be excluded
    //   pkid 7  03-15 Sun  台北 slot 1     previous week — must be excluded
    //   pkid 8  03-16 Mon  新竹 slot 1     another centre, same week — the centre filter
    //
    // Rows are seeded out of date order so the ORDER BY is observable.
    public FakeFeaturedPromoItemRepository Repository { get; } = new FakeFeaturedPromoItemRepository()
        .SeedPromotion(3403, "20251204_SkillTrainAI")
        .SeedPromotion(3423, "20251215_n8n")
        .SeedPromotion(3393, "20251219_GoogleAIseminar")
        .Seed("2026-03-16", 1, 1, 3403, "成為能AI協作的程式設計師", "轉職就業養成班，三大主流語言任你選")
        .Seed("2026-03-16", 1, 2, 3423, "n8n自動化三部曲", "從自動化新手到企業級AI架構師學習路徑")
        .Seed("2026-03-16", 1, 3, 3393, "Google AI工具一次掌握", "不需技術基礎！最新Google AI實戰課程")
        .Seed("2026-03-17", 1, 1, 3423, "n8n自動化三部曲", "從自動化新手到企業級AI架構師學習路徑")
        .Seed("2026-03-22", 1, 1, 3403, "週日也上稿", "週日是一週的最後一天")
        .Seed("2026-03-23", 1, 1, 3403, "下週一", "不屬於這一週")
        .Seed("2026-03-15", 1, 1, 3403, "上週日", "也不屬於這一週")
        .Seed("2026-03-16", 2, 1, 3393, "新竹的第一格", "另一個據點");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IFeaturedPromoItemRepository>();
            services.AddSingleton<IFeaturedPromoItemRepository>(Repository);
        });
    }
}
