using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Application.Services;
using BallastlaneTechStore.Domain.Enums;
using BallastlaneTechStore.Store.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace BallastlaneTechStore.Store.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;
    public CustomersController(ICustomerService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<CustomerDto>> List(
        [FromQuery] CustomerStatus? status, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => _service.ListAsync(status, skip, take, ct);

    [HttpGet("{id:guid}")]
    public Task<CustomerDto> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest body, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(body, CurrentUser.Id(User), ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public Task<CustomerDto> Update(Guid id, [FromBody] UpdateCustomerRequest body, CancellationToken ct)
        => _service.UpdateAsync(id, body, ct);

    [HttpPatch("{id:guid}/status")]
    public Task<CustomerDto> Promote(Guid id, [FromBody] PromoteCustomerRequest body, CancellationToken ct)
        => _service.PromoteAsync(id, body.Status, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
