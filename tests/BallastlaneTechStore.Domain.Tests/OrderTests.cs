using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using FluentAssertions;

namespace BallastlaneTechStore.Domain.Tests;

public class OrderTests
{
    private static readonly DateTime Now = new(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    private static Order NewOrder() => Order.Create("ORD-20260508-0001", Customer, Owner, Now);

    [Fact] public void Create_starts_in_draft_with_zero_total()
    {
        var o = NewOrder();
        o.Status.Should().Be(OrderStatus.Draft);
        o.Total.Should().Be(0);
        o.Items.Should().BeEmpty();
    }

    [Fact] public void AddItem_recomputes_subtotal()
    {
        var o = NewOrder();
        var p1 = Guid.NewGuid();
        o.AddItem(p1, 2, 100m, Now);
        o.Subtotal.Should().Be(200m);
        o.Items.Should().HaveCount(1);
    }

    [Fact] public void AddItem_with_same_product_merges_quantity()
    {
        var o = NewOrder();
        var p = Guid.NewGuid();
        o.AddItem(p, 1, 100m, Now);
        o.AddItem(p, 2, 110m, Now); // same product → merge + reprice
        o.Items.Should().HaveCount(1);
        o.Items.Single().Quantity.Should().Be(3);
        o.Items.Single().UnitPriceSnapshot.Should().Be(110m);
    }

    [Fact] public void ChangeItemQuantity_updates_subtotal()
    {
        var o = NewOrder();
        var p = Guid.NewGuid();
        var line = o.AddItem(p, 1, 50m, Now);
        o.ChangeItemQuantity(line.Id, 4, Now);
        o.Subtotal.Should().Be(200m);
    }

    [Fact] public void RemoveItem_drops_line()
    {
        var o = NewOrder();
        var p = Guid.NewGuid();
        var line = o.AddItem(p, 1, 50m, Now);
        o.RemoveItem(line.Id, Now);
        o.Items.Should().BeEmpty();
        o.Subtotal.Should().Be(0);
    }

    [Fact] public void Cannot_add_items_after_confirm()
    {
        var o = NewOrder();
        var p = Guid.NewGuid();
        o.AddItem(p, 1, 100m, Now);
        o.Confirm(new Dictionary<Guid, decimal> { [p] = 100m }, Now);
        FluentActions.Invoking(() => o.AddItem(p, 1, 100m, Now)).Should().Throw<DomainException>();
    }

    [Fact] public void Confirm_emits_decrement_per_line()
    {
        var o = NewOrder();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        o.AddItem(p1, 2, 100m, Now);
        o.AddItem(p2, 1, 50m, Now);

        var decrements = o.Confirm(new Dictionary<Guid, decimal> { [p1] = 100m, [p2] = 50m }, Now);

        decrements.Should().BeEquivalentTo(new[]
        {
            new StockDecrement(p1, 2),
            new StockDecrement(p2, 1),
        });
        o.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact] public void Confirm_freezes_unit_prices_at_current_catalog_price()
    {
        var o = NewOrder();
        var p = Guid.NewGuid();
        o.AddItem(p, 2, 100m, Now); // line price was 100 when added
        o.Confirm(new Dictionary<Guid, decimal> { [p] = 120m }, Now); // catalog moved to 120
        o.Items.Single().UnitPriceSnapshot.Should().Be(120m);
        o.Subtotal.Should().Be(240m);
    }

    [Fact] public void Confirm_with_no_lines_throws()
        => FluentActions.Invoking(() => NewOrder().Confirm(new Dictionary<Guid, decimal>(), Now))
                        .Should().Throw<DomainException>();

    [Fact] public void Confirm_with_missing_price_throws()
    {
        var o = NewOrder();
        var p = Guid.NewGuid();
        o.AddItem(p, 1, 100m, Now);
        FluentActions.Invoking(() => o.Confirm(new Dictionary<Guid, decimal>(), Now))
                     .Should().Throw<DomainException>();
    }

    [Fact] public void Fulfill_only_from_confirmed()
    {
        var o = NewOrder();
        FluentActions.Invoking(() => o.Fulfill(Now)).Should().Throw<DomainException>();

        var p = Guid.NewGuid();
        o.AddItem(p, 1, 100m, Now);
        o.Confirm(new Dictionary<Guid, decimal> { [p] = 100m }, Now);
        o.Fulfill(Now);
        o.Status.Should().Be(OrderStatus.Fulfilled);
    }

    [Fact] public void Cancel_blocks_fulfilled()
    {
        var o = NewOrder();
        var p = Guid.NewGuid();
        o.AddItem(p, 1, 100m, Now);
        o.Confirm(new Dictionary<Guid, decimal> { [p] = 100m }, Now);
        o.Fulfill(Now);
        FluentActions.Invoking(() => o.Cancel(Now)).Should().Throw<DomainException>();
    }

    [Fact] public void Cancel_from_draft_or_confirmed_is_idempotent()
    {
        var draft = NewOrder();
        draft.Cancel(Now);
        draft.Cancel(Now); // second time is no-op
        draft.Status.Should().Be(OrderStatus.Cancelled);
    }
}
