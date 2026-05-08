using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Application.Common;
using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Application.Services;

public interface IOrderService
{
    Task<PagedResult<OrderSummaryDto>> ListAsync(OrderStatus? status, Guid? customerId, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<PipelineSummary>> GetPipelineSummaryAsync(CancellationToken ct);
    Task<OrderDto> GetAsync(Guid id, CancellationToken ct);
    Task<OrderDto> CreateDraftAsync(CreateOrderRequest request, Guid ownerId, CancellationToken ct);
    Task<OrderDto> AddItemAsync(Guid orderId, AddOrderItemRequest request, CancellationToken ct);
    Task<OrderDto> ChangeItemQuantityAsync(Guid orderId, Guid itemId, int quantity, CancellationToken ct);
    Task<OrderDto> RemoveItemAsync(Guid orderId, Guid itemId, CancellationToken ct);
    Task<OrderDto> ChangeStatusAsync(Guid orderId, OrderStatus next, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly ICustomerRepository _customers;
    private readonly IProductRepository _products;
    private readonly IOrderConfirmationUnitOfWork _confirmUow;
    private readonly IClock _clock;

    public OrderService(
        IOrderRepository orders,
        ICustomerRepository customers,
        IProductRepository products,
        IOrderConfirmationUnitOfWork confirmUow,
        IClock clock)
    {
        _orders = orders; _customers = customers; _products = products; _confirmUow = confirmUow; _clock = clock;
    }

    public async Task<PagedResult<OrderSummaryDto>> ListAsync(OrderStatus? status, Guid? customerId, int skip, int take, CancellationToken ct)
    {
        take = Clamp(take);
        var items = await _orders.ListAsync(status, customerId, skip, take, ct);
        var total = await _orders.CountAsync(status, customerId, ct);
        var customers = await LoadCustomerNamesAsync(items.Select(o => o.CustomerId).Distinct(), ct);
        var dtos = items.Select(o => new OrderSummaryDto(
            o.Id, o.Number, o.CustomerId, customers.GetValueOrDefault(o.CustomerId, "—"),
            o.Status, o.Total, o.CreatedAt)).ToList();
        return new PagedResult<OrderSummaryDto>(dtos, total, skip, take);
    }

    public async Task<IReadOnlyList<PipelineSummary>> GetPipelineSummaryAsync(CancellationToken ct)
    {
        var summary = await _orders.SummariseAsync(ct);
        return Enum.GetValues<OrderStatus>()
            .Select(s => summary.TryGetValue(s, out var v)
                ? new PipelineSummary(s, v.Count, v.Total)
                : new PipelineSummary(s, 0, 0m))
            .ToList();
    }

    public async Task<OrderDto> GetAsync(Guid id, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(id, ct) ?? throw new NotFoundException("Order");
        return await ToDtoAsync(order, ct);
    }

    public async Task<OrderDto> CreateDraftAsync(CreateOrderRequest request, Guid ownerId, CancellationToken ct)
    {
        if (await _customers.GetByIdAsync(request.CustomerId, ct) is null)
            throw new NotFoundException("Customer");
        var number = await _orders.NextOrderNumberAsync(_clock.UtcNow, ct);
        var order = Order.Create(number, request.CustomerId, ownerId, _clock.UtcNow);
        await _orders.AddAsync(order, ct);
        return await ToDtoAsync(order, ct);
    }

    public async Task<OrderDto> AddItemAsync(Guid orderId, AddOrderItemRequest request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId, ct) ?? throw new NotFoundException("Order");
        var product = await _products.GetByIdAsync(request.ProductId, ct) ?? throw new NotFoundException("Product");
        order.AddItem(product.Id, request.Quantity, product.Price, _clock.UtcNow);
        await _orders.ReplaceItemsAsync(order, ct);
        await _orders.UpdateHeaderAsync(order, ct);
        return await ToDtoAsync(order, ct);
    }

    public async Task<OrderDto> ChangeItemQuantityAsync(Guid orderId, Guid itemId, int quantity, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId, ct) ?? throw new NotFoundException("Order");
        order.ChangeItemQuantity(itemId, quantity, _clock.UtcNow);
        await _orders.ReplaceItemsAsync(order, ct);
        await _orders.UpdateHeaderAsync(order, ct);
        return await ToDtoAsync(order, ct);
    }

    public async Task<OrderDto> RemoveItemAsync(Guid orderId, Guid itemId, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId, ct) ?? throw new NotFoundException("Order");
        order.RemoveItem(itemId, _clock.UtcNow);
        await _orders.ReplaceItemsAsync(order, ct);
        await _orders.UpdateHeaderAsync(order, ct);
        return await ToDtoAsync(order, ct);
    }

    public async Task<OrderDto> ChangeStatusAsync(Guid orderId, OrderStatus next, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId, ct) ?? throw new NotFoundException("Order");

        if (next == OrderStatus.Confirmed)
        {
            // Pull current product state to (a) snapshot prices and (b) compute the
            // expected RowVersion for each conditional decrement.
            var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _products.GetManyAsync(productIds, ct);
            var byId = products.ToDictionary(p => p.Id);
            foreach (var pid in productIds)
            {
                if (!byId.ContainsKey(pid)) throw new NotFoundException($"Product {pid}");
            }
            var prices = byId.ToDictionary(kv => kv.Key, kv => kv.Value.Price);
            var versions = byId.ToDictionary(kv => kv.Key, kv => kv.Value.RowVersion);

            order.Confirm(prices, _clock.UtcNow);
            await _confirmUow.ConfirmAsync(order, versions, ct);
            return await ToDtoAsync(order, ct);
        }

        if (next == OrderStatus.Fulfilled)
        {
            order.Fulfill(_clock.UtcNow);
            await _orders.UpdateHeaderAsync(order, ct);
            return await ToDtoAsync(order, ct);
        }

        if (next == OrderStatus.Cancelled)
        {
            order.Cancel(_clock.UtcNow);
            await _orders.UpdateHeaderAsync(order, ct);
            return await ToDtoAsync(order, ct);
        }

        throw new ValidationException($"Status '{next}' is not a valid transition target.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        // Only drafts can be deleted; confirmed/fulfilled/cancelled stay for audit.
        var order = await _orders.GetByIdAsync(id, ct) ?? throw new NotFoundException("Order");
        if (order.Status != OrderStatus.Draft)
            throw new ValidationException("Only draft orders can be deleted.");
        await _orders.DeleteAsync(id, ct);
    }

    private async Task<OrderDto> ToDtoAsync(Order order, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(order.CustomerId, ct);
        var products = order.Items.Count == 0
            ? Array.Empty<Product>()
            : (IReadOnlyList<Product>)await _products.GetManyAsync(order.Items.Select(i => i.ProductId).Distinct().ToList(), ct);
        var byId = products.ToDictionary(p => p.Id);
        var lines = order.Items.Select(i =>
        {
            byId.TryGetValue(i.ProductId, out var p);
            return new OrderItemDto(i.Id, i.ProductId, p?.Sku ?? "—", p?.Name ?? "—",
                                    i.Quantity, i.UnitPriceSnapshot, i.LineTotal);
        }).ToList();

        return new OrderDto(
            order.Id, order.Number, order.CustomerId, customer?.Company ?? "—",
            order.Status, order.Subtotal, order.Tax, order.Total,
            order.OwnerId, order.CreatedAt, order.UpdatedAt, lines);
    }

    private async Task<Dictionary<Guid, string>> LoadCustomerNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var dict = new Dictionary<Guid, string>();
        foreach (var id in ids)
        {
            var c = await _customers.GetByIdAsync(id, ct);
            if (c is not null) dict[id] = c.Company;
        }
        return dict;
    }

    private static int Clamp(int take) => take is <= 0 or > 200 ? 50 : take;
}
