using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Domain.Entities;

public sealed class Order
{
    private readonly List<OrderItem> _items = new();

    public Guid Id { get; private set; }
    public string Number { get; private set; } = default!;
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Tax { get; private set; }
    public decimal Total => Subtotal + Tax;
    public Guid OwnerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public static Order Create(string number, Guid customerId, Guid ownerId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(number)) throw new DomainException("Order number is required.");
        if (customerId == Guid.Empty) throw new DomainException("Customer is required.");
        if (ownerId == Guid.Empty) throw new DomainException("Owner is required.");
        return new Order
        {
            Id = Guid.NewGuid(),
            Number = number.Trim(),
            CustomerId = customerId,
            Status = OrderStatus.Draft,
            OwnerId = ownerId,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
        };
    }

    public static Order Hydrate(Guid id, string number, Guid customerId, OrderStatus status,
        decimal subtotal, decimal tax, Guid ownerId, DateTime createdAt, DateTime updatedAt,
        IEnumerable<OrderItem> items)
    {
        var order = new Order
        {
            Id = id, Number = number, CustomerId = customerId, Status = status,
            Subtotal = subtotal, Tax = tax, OwnerId = ownerId,
            CreatedAt = createdAt, UpdatedAt = updatedAt,
        };
        order._items.AddRange(items);
        return order;
    }

    public OrderItem AddItem(Guid productId, int quantity, decimal currentUnitPrice, DateTime nowUtc)
    {
        RequireDraft();
        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
        {
            existing.ChangeQuantity(existing.Quantity + quantity);
            existing.RepriceTo(currentUnitPrice);
            Recalculate(nowUtc);
            return existing;
        }
        var item = OrderItem.Create(Id, productId, quantity, currentUnitPrice);
        _items.Add(item);
        Recalculate(nowUtc);
        return item;
    }

    public void ChangeItemQuantity(Guid itemId, int quantity, DateTime nowUtc)
    {
        RequireDraft();
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException("Order item not found.");
        item.ChangeQuantity(quantity);
        Recalculate(nowUtc);
    }

    public void RemoveItem(Guid itemId, DateTime nowUtc)
    {
        RequireDraft();
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException("Order item not found.");
        _items.Remove(item);
        Recalculate(nowUtc);
    }

    // Confirms the draft. Returns the per-product stock decrements the application layer
    // must apply atomically — domain stays free of repository concerns. Tax is computed
    // at confirm time (currently 0; placeholder for a real tax engine).
    public IReadOnlyList<StockDecrement> Confirm(IReadOnlyDictionary<Guid, decimal> currentPrices, DateTime nowUtc)
    {
        if (Status != OrderStatus.Draft) throw new DomainException($"Order in {Status} cannot be confirmed.");
        if (_items.Count == 0) throw new DomainException("Cannot confirm an empty order.");

        // Snapshot prices from current product catalog so the order is frozen at confirm time.
        foreach (var item in _items)
        {
            if (!currentPrices.TryGetValue(item.ProductId, out var price))
                throw new DomainException($"Missing current price for product {item.ProductId}.");
            item.RepriceTo(price);
        }

        Status = OrderStatus.Confirmed;
        Recalculate(nowUtc);
        return _items.Select(i => new StockDecrement(i.ProductId, i.Quantity)).ToList();
    }

    public void Fulfill(DateTime nowUtc)
    {
        if (Status != OrderStatus.Confirmed) throw new DomainException($"Order in {Status} cannot be fulfilled.");
        Status = OrderStatus.Fulfilled;
        UpdatedAt = nowUtc;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status is OrderStatus.Fulfilled) throw new DomainException("A fulfilled order cannot be cancelled.");
        if (Status is OrderStatus.Cancelled) return;
        Status = OrderStatus.Cancelled;
        UpdatedAt = nowUtc;
    }

    private void RequireDraft()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException($"Order is {Status}; line items are frozen.");
    }

    private void Recalculate(DateTime nowUtc)
    {
        Subtotal = _items.Sum(i => i.LineTotal);
        Tax = 0m; // placeholder; see PLAN §9 — needs a tax engine for real jurisdictions.
        UpdatedAt = nowUtc;
    }
}

public readonly record struct StockDecrement(Guid ProductId, int Quantity);
