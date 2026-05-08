using BallastlaneTechStore.Application.Common;
using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Application.Services;
using BallastlaneTechStore.Application.Tests.TestSupport;
using BallastlaneTechStore.Domain.Enums;
using FluentAssertions;

namespace BallastlaneTechStore.Application.Tests;

public class CustomerServiceTests
{
    private readonly InMemoryCustomerRepo _repo = new();
    private readonly FixedClock _clock = new();
    private readonly Guid _owner = Guid.NewGuid();

    private CustomerService Sut() => new(_repo, _clock);

    [Fact]
    public async Task Create_then_promote_persists_status()
    {
        var c = await Sut().CreateAsync(new CreateCustomerRequest("Acme", "J", "j@a.io", null), _owner, default);
        var p = await Sut().PromoteAsync(c.Id, CustomerStatus.Prospect, default);
        p.Status.Should().Be(CustomerStatus.Prospect);
    }

    [Fact]
    public async Task Get_unknown_throws()
        => await FluentActions.Invoking(() => Sut().GetAsync(Guid.NewGuid(), default))
                              .Should().ThrowAsync<NotFoundException>();

    [Fact]
    public async Task List_filters_by_status()
    {
        var lead = await Sut().CreateAsync(new CreateCustomerRequest("L", "x", "x@a.io", null), _owner, default);
        var pros = await Sut().CreateAsync(new CreateCustomerRequest("P", "y", "y@a.io", null), _owner, default);
        await Sut().PromoteAsync(pros.Id, CustomerStatus.Prospect, default);

        var leads = await Sut().ListAsync(CustomerStatus.Lead, 0, 50, default);
        leads.Items.Select(c => c.Id).Should().ContainSingle(id => id == lead.Id);
    }
}

public class ProductServiceTests
{
    private readonly InMemoryProductRepo _repo = new();
    private readonly FixedClock _clock = new();

    private ProductService Sut() => new(_repo, _clock);

    [Fact]
    public async Task Duplicate_sku_blocked_at_create()
    {
        await Sut().CreateAsync(new CreateProductRequest("X1", "n", ProductCategory.Cpu, "b", 1m, 1), default);
        await FluentActions.Invoking(() => Sut().CreateAsync(new CreateProductRequest("x1", "n2", ProductCategory.Gpu, "b", 2m, 2), default))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_keeps_same_sku()
    {
        var p = await Sut().CreateAsync(new CreateProductRequest("X1", "n", ProductCategory.Cpu, "b", 1m, 1), default);
        var updated = await Sut().UpdateAsync(p.Id, new UpdateProductRequest("X1", "n2", ProductCategory.Cpu, "b", 2m, 2), default);
        updated.Name.Should().Be("n2");
    }

    [Fact]
    public async Task Low_stock_filter_returns_only_low()
    {
        await Sut().CreateAsync(new CreateProductRequest("LOW", "n", ProductCategory.Gpu, "b", 100m, 2), default);
        await Sut().CreateAsync(new CreateProductRequest("OK", "n2", ProductCategory.Gpu, "b", 100m, 50), default);
        var low = await Sut().ListAsync(null, true, 0, 50, default);
        low.Items.Should().ContainSingle(p => p.Sku == "LOW");
    }
}
