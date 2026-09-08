using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>上稿作業 FeaturedPromoItem CRUD — 首頁每日每據點三個時段的促銷活動.</summary>
[ApiController]
[Route("api/featured-promo-items")]
[Produces("application/json")]
public class FeaturedPromoItemsController : ControllerBase
{
    private readonly IFeaturedPromoItemRepository _repository;

    public FeaturedPromoItemsController(IFeaturedPromoItemRepository repository) => _repository = repository;

    /// <summary>取得所有上稿資料 (約 3 萬筆 — 畫面不使用, 請改用 query).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeaturedPromoItem>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>依據點與「週」查詢 — weekOf 為該週任一天, 伺服器換算成週一到週日.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeaturedPromoItem>>> Query(
        [FromBody] FeaturedPromoItemQuery query,
        CancellationToken cancellationToken)
    {
        var items = await _repository.QueryAsync(query ?? new FeaturedPromoItemQuery(), cancellationToken);
        return Ok(items);
    }

    /// <summary>依主代碼取得單一上稿資料.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeaturedPromoItem>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>新增上稿資料 (同日期、據點、時段已有資料回 409).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeaturedPromoItem>> Create(
        [FromBody] FeaturedPromoItemRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.SlotTakenAsync(request.ScheduleOn!.Value, request.TrainingCenterPkid, request.Slot, null, cancellationToken))
        {
            return SlotTaken(request);
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>更新上稿資料 (主代碼由 body 帶入; 搬到已被佔用的日期、據點、時段回 409).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeaturedPromoItem>> Update(
        [FromBody] FeaturedPromoItemRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Pkid <= 0)
        {
            ModelState.AddModelError(nameof(request.Pkid), "更新時必須提供主代碼。");
            return ValidationProblem(ModelState);
        }

        if (await _repository.SlotTakenAsync(request.ScheduleOn!.Value, request.TrainingCenterPkid, request.Slot, request.Pkid, cancellationToken))
        {
            return SlotTaken(request);
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var item = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(item);
    }

    /// <summary>刪除上稿資料 — 沒有任何資料表參照本表, 不需保護.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>時段往前一格 (Slot − 1; 畫面上的「−」). 目標格已有資料時兩筆對調. 已在第 1 格回 409.</summary>
    [HttpPost("{id:int}/move-up")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<FeaturedPromoItem>> MoveUp(int id, CancellationToken cancellationToken) =>
        Move(id, -1, cancellationToken);

    /// <summary>時段往後一格 (Slot + 1; 畫面上的「+」). 目標格已有資料時兩筆對調. 已在第 3 格回 409.</summary>
    [HttpPost("{id:int}/move-down")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<FeaturedPromoItem>> MoveDown(int id, CancellationToken cancellationToken) =>
        Move(id, +1, cancellationToken);

    private async Task<ActionResult<FeaturedPromoItem>> Move(int id, int delta, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var target = item.Slot + delta;
        if (target < 1 || target > FeaturedPromoItemRequest.SlotCount)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "無法移動",
                Detail = delta < 0 ? "已在第 1 個時段，無法再往前。" : $"已在第 {FeaturedPromoItemRequest.SlotCount} 個時段，無法再往後。"
            });
        }

        var moved = await _repository.MoveToSlotAsync(id, (byte)target, cancellationToken);
        if (!moved)
        {
            return NotFound();
        }

        var updated = await _repository.GetByIdAsync(id, cancellationToken);
        return Ok(updated);
    }

    private ConflictObjectResult SlotTaken(FeaturedPromoItemRequest request) => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "時段重複",
        Detail = $"{request.ScheduleOn:yyyy-MM-dd} 據點 {request.TrainingCenterPkid} 的第 {request.Slot} 個時段已有資料。"
    });
}
