using BallastlaneTechStore.Domain.Common;

namespace BallastlaneTechStore.Domain.Entities;

public sealed class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    // Snapshot of the product price at the time the line was last edited / order confirmed.
    // Decoupling order line price from product price is what stops a later catalog edit
    // from silently re-pricing closed orders.
    public decimal UnitPriceSnapshot { get; private set; }

    public decimal LineTotal => UnitPriceSnapshot * Quantity;

    private OrderItem() { }

    internal static OrderItem Create(Guid orderId, Guid productId, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty) throw new DomainException("Product is required.");
        if (quantity < 1) throw new DomainException("Quantity must be at least 1.");
        if (unitPrice < 0m) throw new DomainException("Unit price cannot be negative.");
        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            Quantity = quantity,
            UnitPriceSnapshot = unitPrice,
        };
    }

    public static OrderItem Hydrate(Guid id, Guid orderId, Guid productId, int quantity, decimal unitPriceSnapshot)
        => new() { Id = id, OrderId = orderId, ProductId = productId, Quantity = quantity, UnitPriceSnapshot = unitPriceSnapshot };

    internal void ChangeQuantity(int quantity)
    {
        if (quantity < 1) throw new DomainException("Quantity must be at least 1.");
        Quantity = quantity;
    }

    internal void RepriceTo(decimal currentPrice)
    {
        if (currentPrice < 0m) throw new DomainException("Unit price cannot be negative.");
        UnitPriceSnapshot = currentPrice;
    }
}
