<h1>
  <img src="web/ballastlane-tech-store-web/public/logo.png" alt="Ballastlane" height="40" align="left" />
  &nbsp;Tech Store
</h1>

A small ERP for a computer-parts store: customers, a product catalog (CPUs, GPUs, RAM,
SSDs, motherboards, PSUs, cases, coolers) and **orders with line items that decrement
stock on confirm**.

Built as a submission for the Ballastlane .NET technical interview.

**Repository:** <https://github.com/andrade-mcp/ballastlane-tech-store>

---

## Table of contents

- [Highlights](#highlights)
- [Architecture](#architecture)
- [Domain model](#domain-model)
- [Tech stack](#tech-stack)
- [Getting started](#getting-started)
- [Project structure](#project-structure)
- [Testing](#testing)
- [Engineering notes](#engineering-notes)
- [GenAI usage](#genai-usage)
- [Roadmap](#roadmap)
- [License](#license)

---

## Highlights

- Two ASP.NET Core Web APIs (Auth + Store) sharing one Application + Infrastructure layer.
- Clean Architecture with strict dependency direction — Domain knows nothing of frameworks.
- **No ORM.** Hand-written SQL via `Npgsql`. **No MediatR.** Plain service classes.
- Embedded SQL migrations applied automatically on startup with an idempotent ledger.
- Optimistic concurrency on stock decrement at order confirmation (per-row `row_version`).
- ~60 tests across four suites — Domain, Application, Infrastructure, API integration.
- React 18 + Vite + Tailwind frontend with light/dark theming and a brand CTA component.

---

## Architecture

```mermaid
flowchart TB
    subgraph Client["Client"]
        UI["React 18 + Vite<br/>Tailwind v3 + react-query"]
    end

    subgraph Backend["Backend — .NET 9"]
        direction TB
        AuthApi["Auth.Api<br/>:5101<br/>register / login / me"]
        StoreApi["Store.Api<br/>:5102<br/>customers · products · orders"]

        subgraph Shared["Shared assemblies"]
            App["Application<br/>use cases · ports · DTOs"]
            Inf["Infrastructure<br/>Npgsql repos · JWT · BCrypt · migrations"]
            Dom["Domain<br/>entities · enums · invariants"]
        end
    end

    DB[("PostgreSQL 16")]

    UI -->|JWT bearer| AuthApi
    UI -->|JWT bearer| StoreApi
    AuthApi --> App
    StoreApi --> App
    App --> Dom
    Inf --> App
    Inf --> Dom
    AuthApi --> Inf
    StoreApi --> Inf
    Inf --> DB
```

Dependency rule is one-way: outer layers depend on inner. `Application` defines ports
(`IOrderRepository`, `IPasswordHasher`, etc.) and `Infrastructure` implements them.
Both APIs are composition roots — controllers stay thin and delegate to application
services.

The two-API split is a literal reading of the brief, which asks for "a second API" for
authentication. It also makes the JWT bearer flow across service boundaries an explicit
part of the design rather than an aside.

---

## Domain model

```mermaid
erDiagram
    USER ||--o{ CUSTOMER : owns
    USER ||--o{ ORDER    : owns
    CUSTOMER ||--o{ ORDER : "places"
    ORDER ||--|{ ORDER_ITEM : contains
    PRODUCT ||--o{ ORDER_ITEM : "referenced by"

    USER {
      uuid id PK
      string email
      string password_hash
      enum role
    }
    CUSTOMER {
      uuid id PK
      string company
      string contact_name
      string email
      enum status "Lead | Prospect | Active | Churned"
    }
    PRODUCT {
      uuid id PK
      string sku
      enum category "Cpu | Gpu | Ram | Ssd | Motherboard | Psu | Case | Cooler"
      decimal price
      int stock_on_hand
      int row_version "optimistic concurrency"
    }
    ORDER {
      uuid id PK
      string number
      enum status "Draft | Confirmed | Fulfilled | Cancelled"
      decimal subtotal
      decimal tax
      decimal total
    }
    ORDER_ITEM {
      uuid id PK
      int quantity
      decimal unit_price_snapshot
    }
```

### Order state machine

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Confirmed : Confirm() — snapshot prices, decrement stock
    Draft --> Cancelled : Cancel()
    Confirmed --> Fulfilled : Fulfill()
    Confirmed --> Cancelled : Cancel()
    Fulfilled --> [*]
    Cancelled --> [*]
```

`Order.Confirm()` is the load-bearing piece of the model:

1. Refuses to confirm a draft with zero lines.
2. Snapshots each line's `UnitPrice` from the current product price so a later catalog
   edit cannot retro-modify a closed order.
3. Returns a list of stock decrements; the application layer applies them in a single
   Postgres transaction with optimistic concurrency on `products.row_version`. Two
   reps confirming overlapping orders for the same SKU? The second one's
   `UPDATE … WHERE row_version = $X` updates zero rows and the transaction throws
   `OutOfStockException`.

Tests for this live in
[`tests/BallastlaneTechStore.Domain.Tests/OrderTests.cs`](tests/BallastlaneTechStore.Domain.Tests/OrderTests.cs)
and
[`tests/BallastlaneTechStore.Api.Tests/StoreApiTests.cs`](tests/BallastlaneTechStore.Api.Tests/StoreApiTests.cs).

---

## Tech stack

| Layer        | Choice                                                                |
|--------------|------------------------------------------------------------------------|
| Language     | C# 13 / .NET 9                                                         |
| Web          | ASP.NET Core Web API, JWT bearer auth                                  |
| Persistence  | PostgreSQL 16, hand-written SQL via `Npgsql` (no EF / Dapper)          |
| Migrations   | Embedded `.sql` resources + idempotent runner                          |
| Auth         | `BCrypt.Net-Next` for hashing, `System.IdentityModel.Tokens.Jwt`       |
| Tests        | xUnit, FluentAssertions, NSubstitute, `WebApplicationFactory`          |
| Frontend     | React 18, TypeScript, Vite, Tailwind v3                                 |
| Data fetching| `@tanstack/react-query` + axios with bearer interceptor                |
| Forms        | `react-hook-form` + `zod`                                              |
| Container    | Docker Compose: `postgres` + `auth-api` + `store-api`                  |

---

## Getting started

### Prerequisites

- .NET 9 SDK
- Node.js 20+
- Docker Desktop

### One-command start (Docker)

```bash
docker compose up -d
```

Brings up:

| Service     | Address                | Notes                                          |
|-------------|------------------------|------------------------------------------------|
| `postgres`  | `localhost:5434`       | host port 5434 to dodge any local pg on 5432   |
| `auth-api`  | `http://localhost:5101`| Swagger at `/swagger`                          |
| `store-api` | `http://localhost:5102`| Swagger at `/swagger`                          |

Migrations and demo seed run automatically on first API startup.

### Frontend

```bash
cd web/ballastlane-tech-store-web
npm install
npm run dev
```

Open <http://localhost:5174>.

### Demo credentials

```
email:    demo@ballastlane.dev
password: Demo!2026
```

The seed loads 10 products, 6 big-tech customers spread across the lifecycle, and 3 orders
in different pipeline states.

### Reset the database

```bash
docker compose down -v
docker compose up -d
```

### Without Docker (Postgres only)

```bash
docker compose up -d postgres
dotnet run --project src/BallastlaneTechStore.Auth.Api
dotnet run --project src/BallastlaneTechStore.Store.Api
```

The frontend reads `VITE_AUTH_API` / `VITE_STORE_API` from `.env`; defaults already point
at `http://localhost:5101` / `http://localhost:5102`.

---

## Project structure

```
src/
  BallastlaneTechStore.Domain/         entities, enums, invariants — no deps
  BallastlaneTechStore.Application/    use cases, DTOs, ports (interfaces)
  BallastlaneTechStore.Infrastructure/ Npgsql repos, JWT, BCrypt, embedded SQL migrations
  BallastlaneTechStore.Auth.Api/       ASP.NET Core Web API — auth surface          (:5101)
  BallastlaneTechStore.Store.Api/      ASP.NET Core Web API — crud + orders         (:5102)
tests/
  BallastlaneTechStore.Domain.Tests/         invariants — order state machine, etc.
  BallastlaneTechStore.Application.Tests/    services with in-memory repo fakes
  BallastlaneTechStore.Infrastructure.Tests/ BCrypt + JWT issuer
  BallastlaneTechStore.Api.Tests/            WebApplicationFactory, both APIs
web/
  ballastlane-tech-store-web/  Vite + React + TypeScript + Tailwind
docker-compose.yml             postgres + both APIs
```

---

## Testing

```bash
dotnet test
```

All suites are self-contained:

- Domain + Application use hand-written in-memory fakes.
- Infrastructure unit tests cover BCrypt and the JWT issuer.
- API integration boots the full ASP.NET host with the in-memory repos via
  `WebApplicationFactory` — no live PostgreSQL required.

Total: ~60 tests, all green.

---

## Engineering notes

### Why no EF / Dapper / MediatR

Forbidden by the brief. Repositories use `Npgsql` (the official PostgreSQL driver, not an
ORM) with hand-written SQL and small mappers. Use cases are plain service classes;
roughly 20 endpoints do not justify a mediator pipeline. In a project without that
constraint, EF Core would be the default choice given the breadth of the model and the
maturity of its migrations story.

### Migrations

Every `.sql` file under
[`src/BallastlaneTechStore.Infrastructure/Persistence/Migrations/`](src/BallastlaneTechStore.Infrastructure/Persistence/Migrations/)
is embedded into the assembly and applied in lexicographic order on API startup, inside a
transaction, with applied filenames tracked in a `__migrations` ledger table. Adding a
migration is dropping a `0002_whatever.sql` into the folder. Idempotent and EF-free.

### Concurrency model

The order confirmation flow is the only place where two clients can race on the same row
(two reps confirming overlapping orders for the same product). The design uses
**optimistic concurrency** on `products.row_version`: the conditional `UPDATE` only
succeeds if the row hasn't moved since the caller read it. A failed update collapses the
whole transaction with `OutOfStockException`, surfacing as `409 Conflict` to the client.

The boundary lives behind `IOrderConfirmationUnitOfWork` so the application layer stays
agnostic of the transaction mechanics.

### NuGet pinning

- `Swashbuckle.AspNetCore` — pinned to **7.x**. The 10.x line rearranges the
  `Microsoft.OpenApi` namespaces and silently breaks `AddSwaggerGen` config.
- `Microsoft.Extensions.*` — pinned to **9.0.0** to match the .NET 9 runtime.

### Frontend theming

Brand-default dark with an explicit light option. The choice persists in `localStorage`
under a versioned key (`blc.theme`) so old eagerly-written values don't pin returning
visitors. Theme tokens are CSS custom properties on `:root` / `.dark` — see
[`web/ballastlane-tech-store-web/src/styles/globals.css`](web/ballastlane-tech-store-web/src/styles/globals.css).

---

## GenAI usage

Required by the brief; also how the project was actually written. Claude Code in the
editor. I use it for boilerplate, test enumeration, framework-syntax recall (tw classes,
npgsql binding, mermaid grammar), refactors when I can spell the constraint out, and
pasting stack traces back at it for triage. Auth, concurrency, schema — I write those
by hand. The model is wrong often enough at that level that pasting costs more than
typing.

A few prompts from this build, more or less verbatim:

> tests for this. xunit fluentassertions. cover the cant-do paths — cant add after
> Confirmed, Confirm with no lines throws, Confirm reprices from a dict of current
> prices, Cancel from Fulfilled throws, Cancel idempotent. one file no class fixtures.

Got most of it back. Tests asserted on `order.Status` after `Confirm()` but missed the
`StockDecrement` list it returns — that's the load-bearing bit. Added the per-line
assertion.

> npgsql parameterised update. decrement products.stock_on_hand by $qty only if
> row_version=$x AND stock>=$qty. bump row_version. return rows affected. no dapper.

OK. Wrapped it in a tx with the order header + items replace, behind
`IOrderConfirmationUnitOfWork` so the application layer stays mockable.

> tw v3 button. 2px orange gradient ring fd450b→fd7f0b, dark inner that wipes right on
> hover (transition-[width] group-hover:w-0), white text on top. like the ballastlane
> site. no shadcn.

First version used `bg-background` for the inner fill — white text vanished in light
mode until hover. Pinned the dark to `#0b0b0b`, switched text to
`text-[#fd450b] dark:text-white group-hover:text-white`. Two iterations.

> tiny pg migration runner. embedded .sql under a prefix, lex order, idempotent
> __migrations ledger, NpgsqlDataSource. ~80 lines.

Lifted it behind `IMigrationRunner` so the test factory can no-op it via
`WebApplicationFactory.ConfigureTestServices`. Otherwise kept.

> ```
> System.IO.IOException: Failed to bind to address http://127.0.0.1:5101: address already in use.
>    at Microsoft.AspNetCore.Server.Kestrel.Core.Internal.AddressBinder...
> ```
> windows. one-liner to kill it.

Got the `Get-NetTCPConnection -LocalPort … | Stop-Process -Force` chain. Used it half a
dozen times during the dev loop. Faster than recalling the cmdlet from memory.

### What I don't do

One-shot scaffolds. "Fix it" replies — I paste the actual compiler error. Trust tests
that pass first time (re-read them; they often assert on properties that don't exist or
mix NSubstitute syntax with Moq idioms). Let the model own architecture — the two-API
split, the `row_version` concurrency design, the `IOrderConfirmationUnitOfWork` port,
the Domain → Application → Infrastructure → Api test ordering, and the brand-default
dark theme came out of the planning pass before any prompt was issued.

---

## Roadmap

Items considered, scoped out of the deliverable, and acknowledged as the next iteration:

- **Stock-movement ledger** — `stock_movements (product_id, qty_delta, reason, order_id?, occurred_at)`
  table to replace the single `stock_on_hand` column. Foundation for the next item.
- **Refund / restock flow** — Cancelling a Confirmed order should release allocated
  stock; left out because doing it correctly requires the ledger above.
- **Refresh tokens** — login currently issues a single 8-hour JWT.
- **Role-based authorisation** — `Role` is on the user model but not enforced; manager-only
  actions (delete, refund) would gate on it.
- Soft delete + audit columns; multi-tenancy (`TenantId` slots in cleanly given the repo
  pattern).
- Tax engine; multi-currency; discount / promo codes; email + invoice generation.
- Redis cache for the catalog (job-spec bonus); rate limiting on `/api/auth/login`.
- API versioning (`/api/v1/*` + `Asp.Versioning.Http`).
- OpenTelemetry export to an OTLP collector.
- CI/CD — GitHub Actions: build + test on push, container build on tag.
- Playwright E2E covering the golden path.

---

## License

Interview material.
