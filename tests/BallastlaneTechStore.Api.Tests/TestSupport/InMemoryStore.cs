using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Api.Tests.TestSupport;

public sealed class InMemoryStore
{
    public Dictionary<Guid, User> Users { get; } = new();
    public Dictionary<Guid, Customer> Customers { get; } = new();
    public Dictionary<Guid, Product> Products { get; } = new();
    public Dictionary<Guid, Order> Orders { get; } = new();
    public int OrderSeq;
}

public sealed class InMemoryUserRepo(InMemoryStore s) : IUserRepository
{
    public Task AddAsync(User u, CancellationToken ct) { s.Users[u.Id] = u; return Task.CompletedTask; }
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(s.Users.GetValueOrDefault(id));
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct)
        => Task.FromResult(s.Users.Values.FirstOrDefault(u => u.Email == email));
}

public sealed class InMemoryCustomerRepo(InMemoryStore s) : ICustomerRepository
{
    public Task AddAsync(Customer c, CancellationToken ct) { s.Customers[c.Id] = c; return Task.CompletedTask; }
    public Task UpdateAsync(Customer c, CancellationToken ct) { s.Customers[c.Id] = c; return Task.CompletedTask; }
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => Task.FromResult(s.Customers.Remove(id));
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(s.Customers.GetValueOrDefault(id));
    public Task<IReadOnlyList<Customer>> ListAsync(CustomerStatus? status, int skip, int take, CancellationToken ct)
    {
        var q = s.Customers.Values.AsEnumerable();
        if (status is { } st) q = q.Where(c => c.Status == st);
        return Task.FromResult<IReadOnlyList<Customer>>(q.OrderByDescending(c => c.CreatedAt).Skip(skip).Take(take).ToList());
    }
    public Task<int> CountAsync(CustomerStatus? status, CancellationToken ct)
    {
        var q = s.Customers.Values.AsEnumerable();
        if (status is { } st) q = q.Where(c => c.Status == st);
        return Task.FromResult(q.Count());
    }
}

public sealed class InMemoryProductRepo(InMemoryStore s) : IProductRepository
{
    public Task AddAsync(Product p, CancellationToken ct) { s.Products[p.Id] = p; return Task.CompletedTask; }
    public Task UpdateAsync(Product p, CancellationToken ct) { s.Products[p.Id] = p; return Task.CompletedTask; }
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => Task.FromResult(s.Products.Remove(id));
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(s.Products.GetValueOrDefault(id));
    public Task<Product?> GetBySkuAsync(string sku, CancellationToken ct)
        => Task.FromResult(s.Products.Values.FirstOrDefault(p => p.Sku == sku));
    public Task<IReadOnlyList<Product>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Product>>(ids.Select(i => s.Products.GetValueOrDefault(i)).Where(p => p is not null).ToList()!);
    public Task<IReadOnlyList<Product>> ListAsync(ProductCategory? cat, bool? lowStock, int skip, int take, CancellationToken ct)
    {
        var q = s.Products.Values.AsEnumerable();
        if (cat is { } c) q = q.Where(p => p.Category == c);
        if (lowStock == true) q = q.Where(p => p.StockOnHand <= 5);
        return Task.FromResult<IReadOnlyList<Product>>(q.OrderBy(p => p.Name).Skip(skip).Take(take).ToList());
    }
    public Task<int> CountAsync(ProductCategory? cat, bool? lowStock, CancellationToken ct)
    {
        var q = s.Products.Values.AsEnumerable();
        if (cat is { } c) q = q.Where(p => p.Category == c);
        if (lowStock == true) q = q.Where(p => p.StockOnHand <= 5);
        return Task.FromResult(q.Count());
    }
    public Task<bool> TryDecrementStockAsync(Guid productId, int qty, int expectedRowVersion, CancellationToken ct)
    {
        if (!s.Products.TryGetValue(productId, out var p) || p.RowVersion != expectedRowVersion || p.StockOnHand < qty)
            return Task.FromResult(false);
        p.DecrementStock(qty);
        s.Products[productId] = Product.Hydrate(p.Id, p.Sku, p.Name, p.Category, p.Brand, p.Price, p.StockOnHand, p.RowVersion + 1, p.CreatedAt, p.UpdatedAt);
        return Task.FromResult(true);
    }
}

public sealed class InMemoryOrderRepo(InMemoryStore s) : IOrderRepository
{
    public Task AddAsync(Order o, CancellationToken ct) { s.Orders[o.Id] = o; return Task.CompletedTask; }
    public Task UpdateHeaderAsync(Order o, CancellationToken ct) { s.Orders[o.Id] = o; return Task.CompletedTask; }
    public Task ReplaceItemsAsync(Order o, CancellationToken ct) { s.Orders[o.Id] = o; return Task.CompletedTask; }
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct) => Task.FromResult(s.Orders.Remove(id));
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(s.Orders.GetValueOrDefault(id));
    public Task<IReadOnlyList<Order>> ListAsync(OrderStatus? status, Guid? customerId, int skip, int take, CancellationToken ct)
    {
        var q = s.Orders.Values.AsEnumerable();
        if (status is { } st) q = q.Where(o => o.Status == st);
        if (customerId is { } cid) q = q.Where(o => o.CustomerId == cid);
        return Task.FromResult<IReadOnlyList<Order>>(q.OrderByDescending(o => o.CreatedAt).Skip(skip).Take(take).ToList());
    }
    public Task<int> CountAsync(OrderStatus? status, Guid? customerId, CancellationToken ct)
    {
        var q = s.Orders.Values.AsEnumerable();
        if (status is { } st) q = q.Where(o => o.Status == st);
        if (customerId is { } cid) q = q.Where(o => o.CustomerId == cid);
        return Task.FromResult(q.Count());
    }
    public Task<IReadOnlyDictionary<OrderStatus, (int Count, decimal Total)>> SummariseAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<OrderStatus, (int, decimal)>>(
            s.Orders.Values.GroupBy(o => o.Status).ToDictionary(g => g.Key, g => (g.Count(), g.Sum(o => o.Total))));
    public Task<string> NextOrderNumberAsync(DateTime nowUtc, CancellationToken ct)
        => Task.FromResult($"ORD-{nowUtc:yyyyMMdd}-{++s.OrderSeq:D4}");
}

public sealed class InMemoryConfirmUow(InMemoryProductRepo products, InMemoryOrderRepo orders) : IOrderConfirmationUnitOfWork
{
    public async Task ConfirmAsync(Order order, IReadOnlyDictionary<Guid, int> expectedRowVersions, CancellationToken ct)
    {
        foreach (var line in order.Items)
        {
            var ok = await products.TryDecrementStockAsync(line.ProductId, line.Quantity, expectedRowVersions[line.ProductId], ct);
            if (!ok)
            {
                var p = await products.GetByIdAsync(line.ProductId, ct);
                throw new OutOfStockException(line.ProductId, line.Quantity, p?.StockOnHand ?? 0);
            }
        }
        await orders.UpdateHeaderAsync(order, ct);
        await orders.ReplaceItemsAsync(order, ct);
    }
}
