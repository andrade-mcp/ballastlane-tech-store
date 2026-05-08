using System.Text;
using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Infrastructure.Auth;
using BallastlaneTechStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BallastlaneTechStore.Api.Tests.TestSupport;

public sealed class TestApiFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    public InMemoryStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=stub;Database=stub;Username=stub;Password=stub",
                ["Jwt:SigningKey"] = TestJwt.SigningKey,
                ["Jwt:Issuer"] = TestJwt.Issuer,
                ["Jwt:Audience"] = TestJwt.Audience,
                ["Jwt:ExpiresMinutes"] = "60",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            Replace<IMigrationRunner>(services, _ => new NoopMigrations());
            Replace<ISeeder>(services, _ => new NoopSeeder());

            services.AddSingleton(Store);
            Replace<IUserRepository>(services, sp => new InMemoryUserRepo(sp.GetRequiredService<InMemoryStore>()));
            Replace<ICustomerRepository>(services, sp => new InMemoryCustomerRepo(sp.GetRequiredService<InMemoryStore>()));
            Replace<IProductRepository>(services, sp => new InMemoryProductRepo(sp.GetRequiredService<InMemoryStore>()));
            Replace<IOrderRepository>(services, sp => new InMemoryOrderRepo(sp.GetRequiredService<InMemoryStore>()));
            Replace<IOrderConfirmationUnitOfWork>(services, sp =>
                new InMemoryConfirmUow(
                    (InMemoryProductRepo)sp.GetRequiredService<IProductRepository>(),
                    (InMemoryOrderRepo)sp.GetRequiredService<IOrderRepository>()));

            // The bearer middleware caches its TokenValidationParameters at registration
            // time from config; PostConfigure rebinds them to the test signing key.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwt.SigningKey));
                o.TokenValidationParameters.ValidIssuer = TestJwt.Issuer;
                o.TokenValidationParameters.ValidAudience = TestJwt.Audience;
            });
            services.PostConfigure<JwtSettings>(j =>
            {
                j.SigningKey = TestJwt.SigningKey;
                j.Issuer = TestJwt.Issuer;
                j.Audience = TestJwt.Audience;
            });
        });
    }

    private static void Replace<T>(IServiceCollection services, Func<IServiceProvider, T> factory) where T : class
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(T)).ToList()) services.Remove(d);
        services.AddScoped(factory);
    }

    private sealed class NoopMigrations : IMigrationRunner { public Task RunAsync(CancellationToken ct) => Task.CompletedTask; }
    private sealed class NoopSeeder : ISeeder { public Task SeedAsync(CancellationToken ct) => Task.CompletedTask; }
}

internal static class TestJwt
{
    public const string SigningKey = "test-only-signing-key-32-bytes-long-pad-pad";
    public const string Issuer = "ballastlane-tech-store";
    public const string Audience = "ballastlane-tech-store-clients";
}
