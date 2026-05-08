using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BallastlaneTechStore.Infrastructure.Persistence;

public interface ISeeder
{
    Task SeedAsync(CancellationToken ct);
}

// Idempotent. Runs after migrations on every API startup; bails out if the demo user is
// already present so a restart doesn't re-seed.
public sealed class Seeder : ISeeder
{
    public const string DemoEmail = "demo@ballastlane.dev";
    public const string DemoPassword = "Demo!2026";

    private readonly IUserRepository _users;
    private readonly ICustomerRepository _customers;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly ILogger<Seeder> _log;

    public Seeder(IUserRepository users, ICustomerRepository customers, IProductRepository products,
        IOrderRepository orders, IPasswordHasher hasher, IClock clock, ILogger<Seeder> log)
    {
        _users = users; _customers = customers; _products = products; _orders = orders;
        _hasher = hasher; _clock = clock; _log = log;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await _users.GetByEmailAsync(DemoEmail, ct) is not null)
        {
            _log.LogInformation("Seed skipped — demo user already present.");
            return;
        }

        var now = _clock.UtcNow;
        var demo = User.Create(DemoEmail, _hasher.Hash(DemoPassword), "Demo Sales Rep", UserRole.Manager, now);
        await _users.AddAsync(demo, ct);

        // Customers: a couple at every lifecycle state so filters have something to show.
        var leads = new[]
        {
            Customer.Create("Helios Workstations", "Nora Halid",  "nora@helios.io",       "+1 415 555 0142", demo.Id, now),
            Customer.Create("Polar Edge Studios",  "Mia Tan",     "mia@polaredge.gg",     "+44 20 7946 0992", demo.Id, now),
        };
        var prospects = new[]
        {
            Customer.Create("Vantablack Renders",  "Eli Park",    "eli@vantablack.studio", "+1 503 555 0179", demo.Id, now),
        };
        var actives = new[]
        {
            Customer.Create("Northwind Datacenters","Sasha Wu",   "sasha@northwind.cloud","+1 206 555 0188", demo.Id, now),
            Customer.Create("OakHill AI Lab",       "Dev Patel",  "dev@oakhill.ai",       "+1 617 555 0117", demo.Id, now),
        };
        var churned = new[]
        {
            Customer.Create("Atlas Cinematics",     "Rae Kim",    "rae@atlascine.media",  null, demo.Id, now),
        };

        foreach (var c in leads) await _customers.AddAsync(c, ct);
        foreach (var c in prospects) { c.PromoteTo(CustomerStatus.Prospect, now); await _customers.AddAsync(c, ct); }
        foreach (var c in actives)
        {
            c.PromoteTo(CustomerStatus.Prospect, now);
            c.PromoteTo(CustomerStatus.Active, now);
            await _customers.AddAsync(c, ct);
        }
        foreach (var c in churned) { c.PromoteTo(CustomerStatus.Churned, now); await _customers.AddAsync(c, ct); }

        var products = new[]
        {
            Product.Create("CPU-AMD-9950X",     "Ryzen 9 9950X",            ProductCategory.Cpu,         "AMD",      649.00m, 18, now),
            Product.Create("CPU-INT-285K",      "Core Ultra 9 285K",        ProductCategory.Cpu,         "Intel",    589.00m, 12, now),
            Product.Create("GPU-NV-RTX5090",    "GeForce RTX 5090",         ProductCategory.Gpu,         "NVIDIA",  1999.00m,  4, now),
            Product.Create("GPU-AMD-RX9070XT",  "Radeon RX 9070 XT",        ProductCategory.Gpu,         "AMD",      699.00m,  9, now),
            Product.Create("RAM-COR-32G6400",   "Vengeance 32GB DDR5-6400", ProductCategory.Ram,         "Corsair",  179.00m, 40, now),
            Product.Create("SSD-SAM-990P-2T",   "990 PRO 2TB NVMe",         ProductCategory.Ssd,         "Samsung",  219.00m, 30, now),
            Product.Create("MB-ASU-X870E",      "ROG Crosshair X870E",      ProductCategory.Motherboard, "ASUS",     549.00m,  7, now),
            Product.Create("PSU-COR-RM1000X",   "RM1000x 1000W 80+ Gold",   ProductCategory.Psu,         "Corsair",  219.00m, 22, now),
            Product.Create("CASE-NZX-H7F",      "H7 Flow",                  ProductCategory.Case,        "NZXT",     129.00m, 15, now),
            Product.Create("COOL-NCT-D15",      "NH-D15 G2",                ProductCategory.Cooler,      "Noctua",   149.00m,  3, now),
        };
        foreach (var p in products) await _products.AddAsync(p, ct);

        // Three orders across the pipeline so the dashboard has signal on day one.
        var northwind = actives[0];
        var oakhill = actives[1];
        var helios = leads[0];

        var draft = Order.Create(await _orders.NextOrderNumberAsync(now, ct), helios.Id, demo.Id, now);
        draft.AddItem(products[2].Id, 1, products[2].Price, now);            // 1× RTX 5090
        draft.AddItem(products[4].Id, 2, products[4].Price, now);            // 2× RAM
        await _orders.AddAsync(draft, ct);
        await _orders.ReplaceItemsAsync(draft, ct);
        await _orders.UpdateHeaderAsync(draft, ct);

        var confirmed = Order.Create(await _orders.NextOrderNumberAsync(now, ct), oakhill.Id, demo.Id, now);
        confirmed.AddItem(products[0].Id, 4, products[0].Price, now);        // 4× Ryzen
        confirmed.AddItem(products[5].Id, 4, products[5].Price, now);        // 4× SSD
        confirmed.Confirm(new Dictionary<Guid, decimal>
        {
            [products[0].Id] = products[0].Price,
            [products[5].Id] = products[5].Price,
        }, now);
        await _orders.AddAsync(confirmed, ct);
        await _orders.ReplaceItemsAsync(confirmed, ct);
        await _orders.UpdateHeaderAsync(confirmed, ct);

        var fulfilled = Order.Create(await _orders.NextOrderNumberAsync(now, ct), northwind.Id, demo.Id, now);
        fulfilled.AddItem(products[3].Id, 2, products[3].Price, now);        // 2× RX 9070 XT
        fulfilled.Confirm(new Dictionary<Guid, decimal> { [products[3].Id] = products[3].Price }, now);
        fulfilled.Fulfill(now);
        await _orders.AddAsync(fulfilled, ct);
        await _orders.ReplaceItemsAsync(fulfilled, ct);
        await _orders.UpdateHeaderAsync(fulfilled, ct);

        _log.LogInformation("Seed complete — {customers} customers, {products} products, 3 orders.",
            leads.Length + prospects.Length + actives.Length + churned.Length, products.Length);
    }
}
