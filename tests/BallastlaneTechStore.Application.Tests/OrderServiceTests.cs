using BallastlaneTechStore.Application.Common;
using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Application.Services;
using BallastlaneTechStore.Application.Tests.TestSupport;
using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using FluentAssertions;

namespace BallastlaneTechStore.Application.Tests;

public class OrderServiceTests
{
    private readonly InMemoryCustomerRepo _customers = new();
    private readonly InMemoryProductRepo _products = new();
    private readonly InMemoryOrderRepo _orders = new();
    private readonly FixedClock _clock = new();
    private readonly Guid _owner = Guid.NewGuid();

    private OrderService Sut() => new(_orders, _customers, _products, new InMemoryConfirmUow(_products, _orders), _clock);

    private async Task<(Customer customer, Product gpu, Product cpu)> SeedAsync(int gpuStock = 5, int cpuStock = 10)
    {
        var customer = Customer.Create("Acme", "Jane", "j@a.io", null, _owner, _clock.UtcNow);
        await _customers.AddAsync(customer, default);
        var gpu = Product.Create("GPU1", "RTX", ProductCategory.Gpu, "NVIDIA", 1500m, gpuStock, _clock.UtcNow);
        var cpu = Product.Create("CPU1", "Ryzen", ProductCategory.Cpu, "AMD", 600m, cpuStock, _clock.UtcNow);
        await _products.AddAsync(gpu, default);
        await _products.AddAsync(cpu, default);
        return (customer, gpu, cpu);
    }

    [Fact]
    public async Task CreateDraft_with_unknown_customer_throws()
    {
        await FluentActions.Invoking(() => Sut().CreateDraftAsync(new CreateOrderRequest(Guid.NewGuid()), _owner, default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateDraft_assigns_human_readable_number()
    {
        var (customer, _, _) = await SeedAsync();
        var dto = await Sut().CreateDraftAsync(new CreateOrderRequest(customer.Id), _owner, default);
        dto.Number.Should().StartWith("ORD-");
        dto.Status.Should().Be(OrderStatus.Draft);
        dto.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AddItem_then_get_round_trips()
    {
        var (customer, gpu, _) = await SeedAsync();
        var draft = await Sut().CreateDraftAsync(new CreateOrderRequest(customer.Id), _owner, default);
        var withLine = await Sut().AddItemAsync(draft.Id, new AddOrderItemRequest(gpu.Id, 2), default);
        withLine.Items.Should().HaveCount(1);
        withLine.Subtotal.Should().Be(3000m);
    }

    [Fact]
    public async Task Confirm_decrements_stock_and_freezes_prices()
    {
        var (customer, gpu, cpu) = await SeedAsync(gpuStock: 5, cpuStock: 10);
        var draft = await Sut().CreateDraftAsync(new CreateOrderRequest(customer.Id), _owner, default);
        await Sut().AddItemAsync(draft.Id, new AddOrderItemRequest(gpu.Id, 2), default);
        await Sut().AddItemAsync(draft.Id, new AddOrderItemRequest(cpu.Id, 3), default);

        var confirmed = await Sut().ChangeStatusAsync(draft.Id, OrderStatus.Confirmed, default);

        confirmed.Status.Should().Be(OrderStatus.Confirmed);
        confirmed.Subtotal.Should().Be(2 * 1500m + 3 * 600m);

        var freshGpu = await _products.GetByIdAsync(gpu.Id, default);
        var freshCpu = await _products.GetByIdAsync(cpu.Id, default);
        freshGpu!.StockOnHand.Should().Be(3);
        freshCpu!.StockOnHand.Should().Be(7);
    }

    [Fact]
    public async Task Confirm_with_insufficient_stock_throws()
    {
        var (customer, gpu, _) = await SeedAsync(gpuStock: 1);
        var draft = await Sut().CreateDraftAsync(new CreateOrderRequest(customer.Id), _owner, default);
        await Sut().AddItemAsync(draft.Id, new AddOrderItemRequest(gpu.Id, 5), default);
        await FluentActions.Invoking(() => Sut().ChangeStatusAsync(draft.Id, OrderStatus.Confirmed, default))
            .Should().ThrowAsync<OutOfStockException>();
    }

    [Fact]
    public async Task Cannot_add_items_after_confirm()
    {
        var (customer, gpu, _) = await SeedAsync();
        var draft = await Sut().CreateDraftAsync(new CreateOrderRequest(customer.Id), _owner, default);
        await Sut().AddItemAsync(draft.Id, new AddOrderItemRequest(gpu.Id, 1), default);
        await Sut().ChangeStatusAsync(draft.Id, OrderStatus.Confirmed, default);
        await FluentActions.Invoking(() => Sut().AddItemAsync(draft.Id, new AddOrderItemRequest(gpu.Id, 1), default))
            .Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Fulfilled_order_cannot_be_deleted()
    {
        var (customer, gpu, _) = await SeedAsync();
        var draft = await Sut().CreateDraftAsync(new CreateOrderRequest(customer.Id), _owner, default);
        await Sut().AddItemAsync(draft.Id, new AddOrderItemRequest(gpu.Id, 1), default);
        await Sut().ChangeStatusAsync(draft.Id, OrderStatus.Confirmed, default);
        await Sut().ChangeStatusAsync(draft.Id, OrderStatus.Fulfilled, default);
        await FluentActions.Invoking(() => Sut().DeleteAsync(draft.Id, default))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Pipeline_summary_returns_one_row_per_status()
    {
        var summary = await Sut().GetPipelineSummaryAsync(default);
        summary.Should().HaveCount(Enum.GetValues<OrderStatus>().Length);
    }
}
