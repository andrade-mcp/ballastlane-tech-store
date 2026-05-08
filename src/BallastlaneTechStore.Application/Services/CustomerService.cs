using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Application.Common;
using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Application.Mapping;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Application.Services;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> ListAsync(CustomerStatus? status, int skip, int take, CancellationToken ct);
    Task<CustomerDto> GetAsync(Guid id, CancellationToken ct);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, Guid ownerId, CancellationToken ct);
    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct);
    Task<CustomerDto> PromoteAsync(Guid id, CustomerStatus status, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _repo;
    private readonly IClock _clock;

    public CustomerService(ICustomerRepository repo, IClock clock) { _repo = repo; _clock = clock; }

    public async Task<PagedResult<CustomerDto>> ListAsync(CustomerStatus? status, int skip, int take, CancellationToken ct)
    {
        take = Clamp(take);
        var items = await _repo.ListAsync(status, skip, take, ct);
        var total = await _repo.CountAsync(status, ct);
        return new PagedResult<CustomerDto>(items.Select(Maps.ToDto).ToList(), total, skip, take);
    }

    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken ct)
        => (await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Customer")).ToDto();

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, Guid ownerId, CancellationToken ct)
    {
        var c = Customer.Create(request.Company, request.ContactName, request.Email, request.Phone, ownerId, _clock.UtcNow);
        await _repo.AddAsync(c, ct);
        return c.ToDto();
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Customer");
        c.Update(request.Company, request.ContactName, request.Email, request.Phone, _clock.UtcNow);
        await _repo.UpdateAsync(c, ct);
        return c.ToDto();
    }

    public async Task<CustomerDto> PromoteAsync(Guid id, CustomerStatus status, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Customer");
        c.PromoteTo(status, _clock.UtcNow);
        await _repo.UpdateAsync(c, ct);
        return c.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        if (!await _repo.DeleteAsync(id, ct)) throw new NotFoundException("Customer");
    }

    private static int Clamp(int take) => take is <= 0 or > 200 ? 50 : take;
}
