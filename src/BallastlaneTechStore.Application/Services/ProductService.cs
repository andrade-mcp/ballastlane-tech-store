using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Application.Common;
using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Application.Mapping;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Application.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> ListAsync(ProductCategory? category, bool? lowStock, int skip, int take, CancellationToken ct);
    Task<ProductDto> GetAsync(Guid id, CancellationToken ct);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct);
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _repo;
    private readonly IClock _clock;

    public ProductService(IProductRepository repo, IClock clock) { _repo = repo; _clock = clock; }

    public async Task<PagedResult<ProductDto>> ListAsync(ProductCategory? category, bool? lowStock, int skip, int take, CancellationToken ct)
    {
        take = Clamp(take);
        var items = await _repo.ListAsync(category, lowStock, skip, take, ct);
        var total = await _repo.CountAsync(category, lowStock, ct);
        return new PagedResult<ProductDto>(items.Select(Maps.ToDto).ToList(), total, skip, take);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct)
        => (await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Product")).ToDto();

    public async Task<ProductDto> CreateAsync(CreateProductRequest r, CancellationToken ct)
    {
        var p = Product.Create(r.Sku, r.Name, r.Category, r.Brand, r.Price, r.StockOnHand, _clock.UtcNow);
        if (await _repo.GetBySkuAsync(p.Sku, ct) is not null)
            throw new ConflictException($"SKU '{p.Sku}' already exists.");
        await _repo.AddAsync(p, ct);
        return p.ToDto();
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest r, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Product");
        p.Update(r.Sku, r.Name, r.Category, r.Brand, r.Price, r.StockOnHand, _clock.UtcNow);
        var existing = await _repo.GetBySkuAsync(p.Sku, ct);
        if (existing is not null && existing.Id != p.Id)
            throw new ConflictException($"SKU '{p.Sku}' already exists.");
        await _repo.UpdateAsync(p, ct);
        return p.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        if (!await _repo.DeleteAsync(id, ct)) throw new NotFoundException("Product");
    }

    private static int Clamp(int take) => take is <= 0 or > 200 ? 50 : take;
}
