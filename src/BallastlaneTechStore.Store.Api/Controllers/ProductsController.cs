using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Application.Services;
using BallastlaneTechStore.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace BallastlaneTechStore.Store.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<ProductDto>> List(
        [FromQuery] ProductCategory? category, [FromQuery] bool? lowStock,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => _service.ListAsync(category, lowStock, skip, take, ct);

    [HttpGet("{id:guid}")]
    public Task<ProductDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest body, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(body, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<ProductDto> Update(Guid id, [FromBody] UpdateProductRequest body, CancellationToken ct)
        => _service.UpdateAsync(id, body, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
