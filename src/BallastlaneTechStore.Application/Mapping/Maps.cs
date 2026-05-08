using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Domain.Entities;

namespace BallastlaneTechStore.Application.Mapping;

internal static class Maps
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Email, u.DisplayName, u.Role);

    public static CustomerDto ToDto(this Customer c) =>
        new(c.Id, c.Company, c.ContactName, c.Email, c.Phone, c.Status, c.OwnerId, c.CreatedAt, c.UpdatedAt);

    public static ProductDto ToDto(this Product p) =>
        new(p.Id, p.Sku, p.Name, p.Category, p.Brand, p.Price, p.StockOnHand, p.RowVersion, p.CreatedAt, p.UpdatedAt);
}
