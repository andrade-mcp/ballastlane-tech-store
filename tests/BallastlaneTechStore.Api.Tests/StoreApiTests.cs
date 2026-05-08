using System.Net;
using System.Net.Http.Json;
using BallastlaneTechStore.Api.Tests.TestSupport;
using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BallastlaneTechStore.Api.Tests;

public class StoreApiTests : IClassFixture<TestApiFactory<StoreApiProgram>>
{
    private readonly TestApiFactory<StoreApiProgram> _factory;
    private readonly HttpClient _client;

    public StoreApiTests(TestApiFactory<StoreApiProgram> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string AuthenticateAsNewUser()
    {
        using var scope = _factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenIssuer>();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = User.Create($"u-{Guid.NewGuid():N}@x.com", hasher.Hash("Pa55word!"), "Test", UserRole.SalesRep, clock.UtcNow);
        users.AddAsync(user, default).GetAwaiter().GetResult();
        return jwt.Issue(user).Token;
    }

    private HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        req.Headers.Authorization = new("Bearer", token);
        return req;
    }

    [Fact]
    public async Task Customers_without_token_returns_401()
        => (await _client.GetAsync("/api/customers")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Customers_with_token_returns_200()
    {
        var t = AuthenticateAsNewUser();
        (await _client.SendAsync(Authed(HttpMethod.Get, "/api/customers", t))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Build_order_then_confirm_decrements_stock()
    {
        var t = AuthenticateAsNewUser();

        var customer = await CreateCustomer(t);
        var product = await CreateProduct(t, "GPU-INT-1", "RTX 5090", ProductCategory.Gpu, 1500m, 5);

        var draft = await CreateDraft(t, customer.Id);
        await AddItem(t, draft.Id, product.Id, 2);

        var confirmed = await ChangeStatus(t, draft.Id, OrderStatus.Confirmed);
        confirmed.Status.Should().Be(OrderStatus.Confirmed);
        confirmed.Subtotal.Should().Be(3000m);

        var fresh = await GetProduct(t, product.Id);
        fresh.StockOnHand.Should().Be(3);
    }

    [Fact]
    public async Task Confirm_with_insufficient_stock_returns_409()
    {
        var t = AuthenticateAsNewUser();
        var customer = await CreateCustomer(t);
        var product = await CreateProduct(t, "GPU-INT-2", "RTX", ProductCategory.Gpu, 100m, 1);
        var draft = await CreateDraft(t, customer.Id);
        await AddItem(t, draft.Id, product.Id, 5);

        var resp = await _client.SendAsync(Authed(HttpMethod.Patch, $"/api/orders/{draft.Id}/status", t,
            new OrderStatusChangeRequest(OrderStatus.Confirmed)));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cannot_add_items_after_confirm()
    {
        var t = AuthenticateAsNewUser();
        var customer = await CreateCustomer(t);
        var product = await CreateProduct(t, "CPU-1", "Ryzen", ProductCategory.Cpu, 500m, 10);
        var draft = await CreateDraft(t, customer.Id);
        await AddItem(t, draft.Id, product.Id, 1);
        await ChangeStatus(t, draft.Id, OrderStatus.Confirmed);

        var resp = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/orders/{draft.Id}/items", t,
            new AddOrderItemRequest(product.Id, 1)));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pipeline_returns_one_row_per_status()
    {
        var t = AuthenticateAsNewUser();
        var resp = await _client.SendAsync(Authed(HttpMethod.Get, "/api/orders/pipeline", t));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<List<PipelineSummary>>();
        body!.Count.Should().Be(Enum.GetValues<OrderStatus>().Length);
    }

    [Fact]
    public async Task Delete_confirmed_order_returns_400()
    {
        var t = AuthenticateAsNewUser();
        var customer = await CreateCustomer(t);
        var product = await CreateProduct(t, "RAM-1", "DDR5", ProductCategory.Ram, 100m, 10);
        var draft = await CreateDraft(t, customer.Id);
        await AddItem(t, draft.Id, product.Id, 1);
        await ChangeStatus(t, draft.Id, OrderStatus.Confirmed);

        var resp = await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/orders/{draft.Id}", t));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<CustomerDto> CreateCustomer(string token)
    {
        var resp = await _client.SendAsync(Authed(HttpMethod.Post, "/api/customers", token,
            new CreateCustomerRequest("Acme", "Jane", $"j-{Guid.NewGuid():N}@a.io", null)));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<CustomerDto>())!;
    }

    private async Task<ProductDto> CreateProduct(string token, string sku, string name, ProductCategory cat, decimal price, int stock)
    {
        var resp = await _client.SendAsync(Authed(HttpMethod.Post, "/api/products", token,
            new CreateProductRequest(sku, name, cat, "TestBrand", price, stock)));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private async Task<OrderDto> CreateDraft(string token, Guid customerId)
    {
        var resp = await _client.SendAsync(Authed(HttpMethod.Post, "/api/orders", token, new CreateOrderRequest(customerId)));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private async Task<OrderDto> AddItem(string token, Guid orderId, Guid productId, int qty)
    {
        var resp = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/orders/{orderId}/items", token,
            new AddOrderItemRequest(productId, qty)));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private async Task<OrderDto> ChangeStatus(string token, Guid orderId, OrderStatus status)
    {
        var resp = await _client.SendAsync(Authed(HttpMethod.Patch, $"/api/orders/{orderId}/status", token,
            new OrderStatusChangeRequest(status)));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private async Task<ProductDto> GetProduct(string token, Guid id)
    {
        var resp = await _client.SendAsync(Authed(HttpMethod.Get, $"/api/products/{id}", token));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<ProductDto>())!;
    }
}
