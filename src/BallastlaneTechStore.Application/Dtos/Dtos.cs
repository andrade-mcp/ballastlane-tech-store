using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Application.Dtos;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);
public sealed record UserDto(Guid Id, string Email, string DisplayName, UserRole Role);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take);

public sealed record CreateCustomerRequest(string Company, string ContactName, string Email, string? Phone);
public sealed record UpdateCustomerRequest(string Company, string ContactName, string Email, string? Phone);
public sealed record PromoteCustomerRequest(CustomerStatus Status);
public sealed record CustomerDto(
    Guid Id, string Company, string ContactName, string Email, string? Phone,
    CustomerStatus Status, Guid OwnerId, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CreateProductRequest(string Sku, string Name, ProductCategory Category, string Brand, decimal Price, int StockOnHand);
public sealed record UpdateProductRequest(string Sku, string Name, ProductCategory Category, string Brand, decimal Price, int StockOnHand);
public sealed record ProductDto(
    Guid Id, string Sku, string Name, ProductCategory Category, string Brand,
    decimal Price, int StockOnHand, int RowVersion, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CreateOrderRequest(Guid CustomerId);
public sealed record AddOrderItemRequest(Guid ProductId, int Quantity);
public sealed record ChangeOrderItemQuantityRequest(int Quantity);
public sealed record OrderStatusChangeRequest(OrderStatus Status);
public sealed record OrderItemDto(Guid Id, Guid ProductId, string ProductSku, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record OrderDto(
    Guid Id, string Number, Guid CustomerId, string CustomerCompany,
    OrderStatus Status, decimal Subtotal, decimal Tax, decimal Total,
    Guid OwnerId, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);
public sealed record OrderSummaryDto(
    Guid Id, string Number, Guid CustomerId, string CustomerCompany,
    OrderStatus Status, decimal Total, DateTime CreatedAt);
public sealed record PipelineSummary(OrderStatus Status, int Count, decimal Total);
