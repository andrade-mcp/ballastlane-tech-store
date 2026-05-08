using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using FluentAssertions;

namespace BallastlaneTechStore.Domain.Tests;

public class ProductTests
{
    private static readonly DateTime Now = new(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc);

    [Fact] public void Create_uppercases_sku()
    {
        var p = Product.Create(" rtx-5090 ", " RTX 5090 ", ProductCategory.Gpu, " NVIDIA ", 1999m, 4, Now);
        p.Sku.Should().Be("RTX-5090");
        p.Brand.Should().Be("NVIDIA");
        p.RowVersion.Should().Be(1);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Rejects_negative_price_or_stock(decimal price, int stock)
        => FluentActions.Invoking(() => Product.Create("X", "n", ProductCategory.Cpu, "b", price, stock, Now))
                        .Should().Throw<DomainException>();

    [Fact] public void DecrementStock_reduces_amount()
    {
        var p = Product.Create("X", "n", ProductCategory.Gpu, "b", 100m, 5, Now);
        p.DecrementStock(2);
        p.StockOnHand.Should().Be(3);
    }

    [Fact] public void DecrementStock_throws_when_insufficient()
    {
        var p = Product.Create("X", "n", ProductCategory.Gpu, "b", 100m, 1, Now);
        FluentActions.Invoking(() => p.DecrementStock(5)).Should().Throw<OutOfStockException>()
            .Where(e => e.Requested == 5 && e.Available == 1);
    }

    [Fact] public void DecrementStock_rejects_zero_or_negative()
    {
        var p = Product.Create("X", "n", ProductCategory.Gpu, "b", 100m, 5, Now);
        FluentActions.Invoking(() => p.DecrementStock(0)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => p.DecrementStock(-1)).Should().Throw<DomainException>();
    }
}
