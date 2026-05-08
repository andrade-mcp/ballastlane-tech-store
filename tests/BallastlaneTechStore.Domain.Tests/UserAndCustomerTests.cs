using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using FluentAssertions;

namespace BallastlaneTechStore.Domain.Tests;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc);

    [Fact] public void Email_normalised()
        => User.Create("  Alice@X.io ", "h", "Alice", UserRole.SalesRep, Now).Email.Should().Be("alice@x.io");

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Invalid_email_rejected(string e)
        => FluentActions.Invoking(() => User.Create(e, "h", "A", UserRole.SalesRep, Now)).Should().Throw<DomainException>();

    [Fact] public void Password_hash_required()
        => FluentActions.Invoking(() => User.Create("a@b.io", "", "A", UserRole.SalesRep, Now)).Should().Throw<DomainException>();
}

public class CustomerTests
{
    private static readonly DateTime Now = new(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact] public void New_customer_starts_as_lead()
        => Customer.Create("Acme", "Jane", "j@a.io", null, Owner, Now).Status.Should().Be(CustomerStatus.Lead);

    [Fact] public void PromoteTo_walks_lead_to_active()
    {
        var c = Customer.Create("Acme", "Jane", "j@a.io", null, Owner, Now);
        c.PromoteTo(CustomerStatus.Prospect, Now);
        c.PromoteTo(CustomerStatus.Active, Now);
        c.Status.Should().Be(CustomerStatus.Active);
    }

    [Fact] public void Cannot_move_backwards()
    {
        var c = Customer.Create("Acme", "Jane", "j@a.io", null, Owner, Now);
        c.PromoteTo(CustomerStatus.Prospect, Now);
        FluentActions.Invoking(() => c.PromoteTo(CustomerStatus.Lead, Now)).Should().Throw<DomainException>();
    }

    [Fact] public void Churned_is_terminal()
    {
        var c = Customer.Create("Acme", "Jane", "j@a.io", null, Owner, Now);
        c.PromoteTo(CustomerStatus.Churned, Now);
        FluentActions.Invoking(() => c.PromoteTo(CustomerStatus.Active, Now)).Should().Throw<DomainException>();
    }
}
