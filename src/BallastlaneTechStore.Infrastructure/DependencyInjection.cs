using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Infrastructure.Auth;
using BallastlaneTechStore.Infrastructure.Common;
using BallastlaneTechStore.Infrastructure.Persistence;
using BallastlaneTechStore.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BallastlaneTechStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(cs).Build());
        services.Configure<JwtSettings>(config.GetSection(JwtSettings.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderConfirmationUnitOfWork, OrderConfirmationUnitOfWork>();

        services.AddSingleton<IMigrationRunner, MigrationRunner>();
        services.AddScoped<ISeeder, Seeder>();
        return services;
    }
}
