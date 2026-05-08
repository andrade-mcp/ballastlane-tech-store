using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Application.Services;
using BallastlaneTechStore.Domain.Enums;
using BallastlaneTechStore.Store.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace BallastlaneTechStore.Store.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _service;
    public OrdersController(IOrderService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<OrderSummaryDto>> List(
        [FromQuery] OrderStatus? status, [FromQuery] Guid? customerId,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => _service.ListAsync(status, customerId, skip, take, ct);

    [HttpGet("pipeline")]
    public Task<IReadOnlyList<PipelineSummary>> Pipeline(CancellationToken ct)
        => _service.GetPipelineSummaryAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<OrderDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderRequest body, CancellationToken ct)
    {
        var dto = await _service.CreateDraftAsync(body, CurrentUser.Id(User), ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPost("{id:guid}/items")]
    public Task<OrderDto> AddItem(Guid id, [FromBody] AddOrderItemRequest body, CancellationToken ct)
        => _service.AddItemAsync(id, body, ct);

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    public Task<OrderDto> ChangeItemQty(Guid id, Guid itemId, [FromBody] ChangeOrderItemQuantityRequest body, CancellationToken ct)
        => _service.ChangeItemQuantityAsync(id, itemId, body.Quantity, ct);

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public Task<OrderDto> RemoveItem(Guid id, Guid itemId, CancellationToken ct)
        => _service.RemoveItemAsync(id, itemId, ct);

    [HttpPatch("{id:guid}/status")]
    public Task<OrderDto> ChangeStatus(Guid id, [FromBody] OrderStatusChangeRequest body, CancellationToken ct)
        => _service.ChangeStatusAsync(id, body.Status, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
