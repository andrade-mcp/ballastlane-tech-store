using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; private set; }
    public string Sku { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public ProductCategory Category { get; private set; }
    public string Brand { get; private set; } = default!;
    public decimal Price { get; private set; }
    public int StockOnHand { get; private set; }
    // Optimistic-concurrency token. Bumped on every persisted update; the SQL UPDATE
    // includes "where row_version = $X" so two concurrent confirms can't both decrement.
    public int RowVersion { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Product() { }

    public static Product Create(string sku, string name, ProductCategory category, string brand,
        decimal price, int stockOnHand, DateTime nowUtc)
    {
        Guard(sku, name, brand, price, stockOnHand);
        return new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Category = category,
            Brand = brand.Trim(),
            Price = price,
            StockOnHand = stockOnHand,
            RowVersion = 1,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
        };
    }

    public static Product Hydrate(Guid id, string sku, string name, ProductCategory category, string brand,
        decimal price, int stockOnHand, int rowVersion, DateTime createdAt, DateTime updatedAt)
        => new()
        {
            Id = id, Sku = sku, Name = name, Category = category, Brand = brand,
            Price = price, StockOnHand = stockOnHand, RowVersion = rowVersion,
            CreatedAt = createdAt, UpdatedAt = updatedAt,
        };

    public void Update(string sku, string name, ProductCategory category, string brand,
        decimal price, int stockOnHand, DateTime nowUtc)
    {
        Guard(sku, name, brand, price, stockOnHand);
        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        Category = category;
        Brand = brand.Trim();
        Price = price;
        StockOnHand = stockOnHand;
        UpdatedAt = nowUtc;
    }

    // Reduce stock for a confirmed order line. Does not bump RowVersion — the repo does
    // that as part of the conditional UPDATE so concurrency is enforced at the DB.
    public void DecrementStock(int qty)
    {
        if (qty <= 0) throw new DomainException("Decrement quantity must be positive.");
        if (qty > StockOnHand) throw new OutOfStockException(Id, qty, StockOnHand);
        StockOnHand -= qty;
    }

    private static void Guard(string sku, string name, string brand, decimal price, int stockOnHand)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new DomainException("SKU is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Name is required.");
        if (string.IsNullOrWhiteSpace(brand)) throw new DomainException("Brand is required.");
        if (price < 0m) throw new DomainException("Price cannot be negative.");
        if (stockOnHand < 0) throw new DomainException("Stock cannot be negative.");
    }
}
