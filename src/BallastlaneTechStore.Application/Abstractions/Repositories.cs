using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string normalisedEmail, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
}

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Customer>> ListAsync(CustomerStatus? status, int skip, int take, CancellationToken ct);
    Task<int> CountAsync(CustomerStatus? status, CancellationToken ct);
    Task AddAsync(Customer customer, CancellationToken ct);
    Task UpdateAsync(Customer customer, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct);
    Task<IReadOnlyList<Product>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<Product>> ListAsync(ProductCategory? category, bool? lowStock, int skip, int take, CancellationToken ct);
    Task<int> CountAsync(ProductCategory? category, bool? lowStock, CancellationToken ct);
    Task AddAsync(Product product, CancellationToken ct);
    Task UpdateAsync(Product product, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    // Conditional decrement using row_version. Returns false if the version moved or
    // stock would go negative — the caller should reload + retry or surface OutOfStock.
    Task<bool> TryDecrementStockAsync(Guid productId, int qty, int expectedRowVersion, CancellationToken ct);
}

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Order>> ListAsync(OrderStatus? status, Guid? customerId, int skip, int take, CancellationToken ct);
    Task<int> CountAsync(OrderStatus? status, Guid? customerId, CancellationToken ct);
    Task<IReadOnlyDictionary<OrderStatus, (int Count, decimal Total)>> SummariseAsync(CancellationToken ct);
    Task<string> NextOrderNumberAsync(DateTime nowUtc, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
    Task UpdateHeaderAsync(Order order, CancellationToken ct);
    Task ReplaceItemsAsync(Order order, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

// Lets the OrderService run "decrement N products + persist order header" inside a single
// Postgres transaction. Implementation lives in Infrastructure.
public interface IOrderConfirmationUnitOfWork
{
    Task ConfirmAsync(Order order, IReadOnlyDictionary<Guid, int> expectedRowVersions, CancellationToken ct);
}
