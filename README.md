# Tech Store

Small ERP for a computer-parts store: customers, a product catalog (CPUs, GPUs, RAM, SSDs,
motherboards, PSUs, cases, coolers) and **orders with line items that decrement stock on
confirm**. Built for the Ballastlane .NET technical interview.

**Repository:** <https://github.com/andrade-mcp/ballastlane-tech-store>

> Doubles as my submission write-up — thought process, design choices, GenAI usage, and the
> things I deliberately left out are all here. No separate slide deck.

## Why this shape

The brief asks for a CRUD app with two APIs (auth + business), Clean Architecture, no EF /
Dapper / MediatR, TDD, and a frontend. I picked a domain that would force me to build
something more interesting than the strict minimum: **orders with line items + a real
stock decrement on confirm**. That gives me an actual state machine to test against, an
optimistic-concurrency story to demo, and a UX that maps to a recognisable workflow rather
than yet another generic CRUD grid.

### User story driving the work

> As a sales rep at a computer-parts distributor, I capture a customer, build a draft
> order from the catalog, and confirm it — at which point stock is allocated — so I can
> close sales without overselling inventory.

## Architecture

Standard Clean Architecture; four backend projects + one frontend:

```
src/
  BallastlaneTechStore.Domain/         entities, enums, invariants — no deps
  BallastlaneTechStore.Application/    use cases, DTOs, ports (interfaces)
  BallastlaneTechStore.Infrastructure/ Npgsql repos, JWT, BCrypt, embedded SQL migrations
  BallastlaneTechStore.Auth.Api/       small Web API: register/login/me  (port 5101)
  BallastlaneTechStore.Store.Api/      crud + ordering Web API           (port 5102)
tests/
  BallastlaneTechStore.Domain.Tests/        invariants — mostly the order state machine
  BallastlaneTechStore.Application.Tests/   services with in-memory repo fakes
  BallastlaneTechStore.Infrastructure.Tests/ BCrypt + JWT issuer
  BallastlaneTechStore.Api.Tests/           WebApplicationFactory, both APIs end-to-end
web/
  ballastlane-tech-store-web/  Vite + React 18 + TypeScript + Tailwind v3
docker-compose.yml             postgres + both APIs
```

### Two API projects on purpose

The spec asks for "a second API" for auth, so I read it literally: two `Program.cs`, two
Swagger surfaces, one shared JWT signing key. It demonstrates the bearer flow across
service boundaries, which is what you'd build in production anyway. In a real codebase I'd
consolidate unless the auth boundary needed independent scaling or a different security
profile.

### No EF / Dapper / MediatR

By spec. Repos use `Npgsql` (the driver, not an ORM) with hand-written SQL and small
mappers. Use cases are plain service classes; ~20 endpoints don't justify a mediator
pipeline. If the spec hadn't forbidden it I'd reach for EF for the breadth of the model
and the migrations story, or Dapper if I cared more about hand-tuned SQL.

### Migrations

Embedded SQL files under
[`src/BallastlaneTechStore.Infrastructure/Persistence/Migrations/`](src/BallastlaneTechStore.Infrastructure/Persistence/Migrations/),
applied on API startup by a tiny runner that tracks applied filenames in a `__migrations`
ledger table inside a transaction. Idempotent. Adding a new migration = drop a
`0002_whatever.sql` in the folder; next API restart picks it up. No EF anywhere on the
codepath.

## What's interesting in the domain

The load-bearing piece is `Order.Confirm()`:

1. Refuses to confirm an empty draft.
2. Snapshots each line's `UnitPrice` from the current product price (so a later catalog
   edit doesn't retro-modify a closed order).
3. Returns a list of stock decrements; the application layer applies them in a single
   Postgres transaction with optimistic concurrency on each `products.row_version`. Two
   reps confirming overlapping orders for the same SKU? The second one's
   `UPDATE … WHERE row_version = $X` updates zero rows and we throw `OutOfStockException`.

Tests for this live in
[`tests/BallastlaneTechStore.Domain.Tests/OrderTests.cs`](tests/BallastlaneTechStore.Domain.Tests/OrderTests.cs)
and the API-level smoke is in
[`tests/BallastlaneTechStore.Api.Tests/StoreApiTests.cs`](tests/BallastlaneTechStore.Api.Tests/StoreApiTests.cs).

## How I built it

1. One-page plan first — user story, entity list, build order, the things I was
   deliberately leaving out. Kept locally; not in the repo.
2. Solution + project refs + NuGet pins. Pinned `Swashbuckle.AspNetCore` to **7.x**
   because 10.x rearranges the `Microsoft.OpenApi` namespaces and silently breaks the
   `AddSwaggerGen` config below. Pinned all `Microsoft.Extensions.*` to **9.0.0** to
   match the runtime. Both bites I've taken before.
3. Domain first, TDD: invariants drove the entity shape.
   `Order_cannot_add_items_after_Confirmed` was written before `Order.AddItem`. Method
   signatures fall out of the test.
4. Application services with hand-rolled in-memory fakes for the repos. NSubstitute only
   for the truly stateless ports (`IPasswordHasher`, `IJwtTokenIssuer`).
5. Infrastructure last — Npgsql repos, embedded SQL migrations applied on startup.
6. Two thin Web API projects sharing the same Application + Infrastructure assemblies.
7. React frontend with a small theme system (light + dark, brand-default dark),
   `react-query` for server state, `react-hook-form` for forms.

## Stack

- **Backend** — .NET 9, ASP.NET Core Web API, Clean Architecture, Npgsql 9,
  BCrypt.Net-Next, JWT bearer.
- **DB** — PostgreSQL 16 in Docker. Embedded SQL migrations with a custom runner.
- **Tests** — xUnit, FluentAssertions, NSubstitute, `WebApplicationFactory` for
  integration. ~60 tests, all green, no live DB needed for `dotnet test`.
- **Frontend** — React 18, TypeScript, Vite, Tailwind v3, react-router, react-query,
  react-hook-form + zod.

## Quick start

Prereqs: **.NET 9 SDK**, **Node 20+**, **Docker Desktop**.

```bash
docker compose up -d                       # postgres + both apis
cd web/ballastlane-tech-store-web
npm install && npm run dev                 # opens http://localhost:5174
```

Demo credentials (seeded on first API startup):

```
email:    demo@ballastlane.dev
password: Demo!2026
```

Postgres is exposed on host port **5434** to dodge any local Postgres on 5432.
To wipe and re-seed: `docker compose down -v && docker compose up -d`.

### Without Docker

```bash
docker compose up -d postgres
dotnet run --project src/BallastlaneTechStore.Auth.Api
dotnet run --project src/BallastlaneTechStore.Store.Api
```

The frontend reads `VITE_AUTH_API` / `VITE_STORE_API` from `.env`; defaults already point
at `http://localhost:5101` / `http://localhost:5102`.

## Tests

```bash
dotnet test
```

All test suites are pure: domain + application use in-memory fakes; api integration tests
boot the full ASP.NET host with in-memory repos via `WebApplicationFactory`. No live
Postgres needed.

## GenAI usage

Required by the brief; also how I work day-to-day. I use Claude Code as my primary
assistant for grunt work — boilerplate, test-case enumeration, refactoring tedious lines,
recalling Tailwind class strings I've forgotten. I do the architecture, the domain
modelling, the test-first ordering, and every code-review decision myself.

A few representative prompts from this exercise (all pasted with the relevant file already
in scope, never from cold):

**1. Generating xUnit cases against a hand-written entity**

> Here's `Order.cs` (pasted). Write xUnit tests with FluentAssertions covering: cannot add
> items after Confirmed, Confirm with no lines throws, Confirm freezes the
> `UnitPriceSnapshot` from a `Dictionary<Guid, decimal>` of current prices, Cancel from
> Fulfilled throws, Cancel is idempotent. Use
> `FluentActions.Invoking(...).Should().Throw<DomainException>()` style. One file, no
> setup ceremony.

What I changed afterwards: added the `StockDecrement` return-value assertions. The model
initially only checked `order.Stage` after `Confirm()` and missed the per-line decrement
output, which is the load-bearing bit of the method. Caught it on review.

**2. Building the conditional decrement SQL**

> Write an Npgsql command that decrements `products.stock_on_hand` by `$qty` only if
> `row_version = $expected` and `stock_on_hand >= $qty`, then bumps `row_version` and
> returns the row count. No EF, no Dapper. Hand-written parameterised SQL.

What I changed: AI gave me a single statement; I wrapped it in a transaction with the
order header + items replace, and put the whole thing behind a port
(`IOrderConfirmationUnitOfWork`) so it stays mockable from the application layer. The
port idea was mine — AI suggested calling the repo directly from the service.

**3. Tailwind for the brand button**

> Need a Tailwind button matching this spec: 2px orange gradient ring
> (`from-[#fd450b] to-[#fd7f0b]`), inner dark fill that "wipes" rightward on hover via
> `transition-[width] group-hover:w-0`, white text on top. About 20 lines of JSX.

What I changed: the first version used `bg-background` for the inner fill, which is
theme-driven via a CSS variable; that produced white-text-on-white in light mode
(invisible until hover). Switched to a pinned `#0b0b0b` for dark + theme-aware text
colour (orange on light, white on dark, both go to white-on-orange on hover).

**4. Migration runner skeleton**

> Sketch a tiny migration runner for Postgres: enumerate embedded `.sql` resources
> matching a prefix, apply pending ones in lex order inside a transaction, track applied
> filenames in a `__migrations` table. Use `NpgsqlDataSource`. ~80 lines, no external
> deps.

What I changed: added the idempotent ledger creation inside the runner (the AI version
assumed the table already existed) and made it `IMigrationRunner` so the test factory can
swap it for a no-op. The interface separation came from a previous project where I'd hit
the same WebApplicationFactory pain.

### Validation pattern

For every AI-generated chunk:

- Read it before pasting. Always.
- Grep for hidden `using Microsoft.EntityFrameworkCore` — the model leaks it even when
  told "no EF". Banned by spec, easy to miss in review.
- Run the tests it wrote. If they pass on the first try, I'm suspicious — usually they
  assert on a property that doesn't exist or use NSubstitute syntax for a Moq mock.
- Re-prompt with the actual error, not "fix it". `error CS0246: type 'X' not found` is a
  far better prompt than "didn't compile".
- JWT issuance code I always rewrite by hand. AI gets clock skew, audience claims, and
  signing-key bytes subtly wrong often enough that pasting isn't worth the diagnostic
  time later.

### What AI did NOT pick

The two-API split, the optimistic-concurrency design on `products.row_version`, the
`IOrderConfirmationUnitOfWork` boundary, the test ordering
(Domain → Application → Infrastructure → Api), the brand-default dark theme. Those came
out of the plan I wrote before opening the editor.

## What I deliberately left out (talking points)

The honest "I'd ship this next" list:

- **Stock-movement ledger** — current `stock_on_hand` is a single number. Real ERP needs
  `stock_movements (product_id, qty_delta, reason, order_id?, occurred_at)` for audit and
  to support the refund flow below.
- **Refund / restock flow** — Cancelling a Confirmed order *should* release allocated
  stock. Left out because doing it correctly requires the ledger above.
- **Refresh tokens** — login returns a single 8-hour JWT.
- **Role-based authorisation** — `Role` is on the user but not enforced; manager-only
  actions (delete, refund) would gate on it.
- **Soft delete + audit columns** — DELETE is hard delete; no who-changed-what trail.
- **Multi-tenancy** — single-org. `TenantId` slots in cleanly given the repo pattern.
- **Tax engine** — `tax = 0` placeholder. Real tax depends on jurisdiction + product class.
- **Multi-currency**, **discount/promo codes**, **email + invoice generation**.
- **Redis cache** for the catalog (job-spec bonus). Catalog is a low-write/hot-read fit.
- **Rate limiting** on `/api/auth/login` (per-IP sliding window).
- **API versioning** (`/api/v1/*` + `Asp.Versioning.Http`).
- **OpenTelemetry** with an OTLP exporter to a collector.
- **CI/CD** — GitHub Actions: build + test on push, container build on tag.
- **Playwright E2E** — golden path: login → create customer → build order → confirm →
  see stock decrement on `/products`.

## License

Interview material — use as you see fit.
