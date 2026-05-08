using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Application.Tests.TestSupport;

public sealed class FixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc);
}

public sealed class InMemoryUserRepo : IUserRepository
{
    private readonly Dictionary<Guid, User> _byId = new();
    public Task AddAsync(User u, CancellationToken ct) { _byId[u.Id] = u; return Task.CompletedTask; }
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_byId.GetValueOrDefault(id));
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct)
        => Task.FromResult(_byId.Values.FirstOrDefault(u => u.Email == email));
}

public sealed class InMemoryCustomerRepo : ICustomerRepository
{
    private readonly Dictionary<Guid, Customer> _byId = new();
    public Task AddAsync(Customer c, CancellationToken ct) { _byId[c.Id] = c; return Task.CompletedTask; }
    public Task UpdateAsync(Customer c, CancellationToken ct) { _byId[c.Id] = c; return Task.CompletedTask; }
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => Task.FromResult(_byId.Remove(id));
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_byId.GetValueOrDefault(id));
    public Task<IReadOnlyList<Customer>> ListAsync(CustomerStatus? status, int skip, int take, CancellationToken ct)
    {
        var q = _byId.Values.AsEnumerable();
        if (status is { } s) q = q.Where(c => c.Status == s);
        return Task.FromResult<IReadOnlyList<Customer>>(q.OrderByDescending(c => c.CreatedAt).Skip(skip).Take(take).ToList());
    }
    public Task<int> CountAsync(CustomerStatus? status, CancellationToken ct)
    {
        var q = _byId.Values.AsEnumerable();
        if (status is { } s) q = q.Where(c => c.Status == s);
        return Task.FromResult(q.Count());
    }
}

public sealed class InMemoryProductRepo : IProductRepository
{
    private readonly Dictionary<Guid, Product> _byId = new();
    public IReadOnlyDictionary<Guid, Product> Snapshot => _byId;
    public Task AddAsync(Product p, CancellationToken ct) { _byId[p.Id] = p; return Task.CompletedTask; }
    public Task UpdateAsync(Product p, CancellationToken ct) { _byId[p.Id] = p; return Task.CompletedTask; }
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => Task.FromResult(_byId.Remove(id));
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_byId.GetValueOrDefault(id));
    public Task<Product?> GetBySkuAsync(string sku, CancellationToken ct)
        => Task.FromResult(_byId.Values.FirstOrDefault(p => p.Sku == sku));
    public Task<IReadOnlyList<Product>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Product>>(ids.Select(i => _byId.GetValueOrDefault(i)).Where(p => p is not null).ToList()!);
    public Task<IReadOnlyList<Product>> ListAsync(ProductCategory? category, bool? lowStock, int skip, int take, CancellationToken ct)
    {
        var q = _byId.Values.AsEnumerable();
        if (category is { } c) q = q.Where(p => p.Category == c);
        if (lowStock == true) q = q.Where(p => p.StockOnHand <= 5);
        return Task.FromResult<IReadOnlyList<Product>>(q.OrderBy(p => p.Name).Skip(skip).Take(take).ToList());
    }
    public Task<int> CountAsync(ProductCategory? category, bool? lowStock, CancellationToken ct)
    {
        var q = _byId.Values.AsEnumerable();
        if (category is { } c) q = q.Where(p => p.Category == c);
        if (lowStock == true) q = q.Where(p => p.StockOnHand <= 5);
        return Task.FromResult(q.Count());
    }
    public Task<bool> TryDecrementStockAsync(Guid productId, int qty, int expectedRowVersion, CancellationToken ct)
    {
        if (!_byId.TryGetValue(productId, out var p) || p.RowVersion != expectedRowVersion || p.StockOnHand < qty)
            return Task.FromResult(false);
        p.DecrementStock(qty);
        // Replace with hydrated copy bumping row version (since real entity has private setter).
        var bumped = Product.Hydrate(p.Id, p.Sku, p.Name, p.Category, p.Brand, p.Price, p.StockOnHand, p.RowVersion + 1, p.CreatedAt, p.UpdatedAt);
        _byId[productId] = bumped;
        return Task.FromResult(true);
    }
}

public sealed class InMemoryOrderRepo : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _byId = new();
    public IReadOnlyDictionary<Guid, Order> Snapshot => _byId;

    public Task AddAsync(Order o, CancellationToken ct) { _byId[o.Id] = o; return Task.CompletedTask; }
    public Task UpdateHeaderAsync(Order o, CancellationToken ct) { _byId[o.Id] = o; return Task.CompletedTask; }
    public Task ReplaceItemsAsync(Order o, CancellationToken ct) { _byId[o.Id] = o; return Task.CompletedTask; }
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => Task.FromResult(_byId.Remove(id));
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_byId.GetValueOrDefault(id));
    public Task<IReadOnlyList<Order>> ListAsync(OrderStatus? status, Guid? customerId, int skip, int take, CancellationToken ct)
    {
        var q = _byId.Values.AsEnumerable();
        if (status is { } s) q = q.Where(o => o.Status == s);
        if (customerId is { } cid) q = q.Where(o => o.CustomerId == cid);
        return Task.FromResult<IReadOnlyList<Order>>(q.OrderByDescending(o => o.CreatedAt).Skip(skip).Take(take).ToList());
    }
    public Task<int> CountAsync(OrderStatus? status, Guid? customerId, CancellationToken ct)
    {
        var q = _byId.Values.AsEnumerable();
        if (status is { } s) q = q.Where(o => o.Status == s);
        if (customerId is { } cid) q = q.Where(o => o.CustomerId == cid);
        return Task.FromResult(q.Count());
    }
    public Task<IReadOnlyDictionary<OrderStatus, (int Count, decimal Total)>> SummariseAsync(CancellationToken ct)
    {
        var dict = _byId.Values.GroupBy(o => o.Status)
            .ToDictionary(g => g.Key, g => (g.Count(), g.Sum(o => o.Total)));
        return Task.FromResult<IReadOnlyDictionary<OrderStatus, (int, decimal)>>(dict);
    }
    public Task<string> NextOrderNumberAsync(DateTime nowUtc, CancellationToken ct)
    {
        var n = _byId.Count + 1;
        return Task.FromResult($"ORD-{nowUtc:yyyyMMdd}-{n:D4}");
    }
}

// Test-only confirmation UoW: applies the conditional decrement against the in-memory
// product repo and persists the order in one go. Mirrors what the real Postgres impl does.
public sealed class InMemoryConfirmUow : IOrderConfirmationUnitOfWork
{
    private readonly InMemoryProductRepo _products;
    private readonly InMemoryOrderRepo _orders;
    public InMemoryConfirmUow(InMemoryProductRepo products, InMemoryOrderRepo orders)
    {
        _products = products; _orders = orders;
    }

    public async Task ConfirmAsync(Order order, IReadOnlyDictionary<Guid, int> expectedRowVersions, CancellationToken ct)
    {
        foreach (var line in order.Items)
        {
            var ok = await _products.TryDecrementStockAsync(line.ProductId, line.Quantity, expectedRowVersions[line.ProductId], ct);
            if (!ok)
            {
                var product = await _products.GetByIdAsync(line.ProductId, ct);
                throw new OutOfStockException(line.ProductId, line.Quantity, product?.StockOnHand ?? 0);
            }
        }
        await _orders.UpdateHeaderAsync(order, ct);
        await _orders.ReplaceItemsAsync(order, ct);
    }
}
