# Ledgr

Inventory and order API for a single warehouse, with an append-only stock ledger.

Every quantity change goes through `Product.ApplyMovement`, which writes the ledger row in the
same call. Orders check and deduct stock inside one transaction, and products and orders carry a
concurrency token, so a checkout that loses a race gets `409` instead of overwriting a newer
stock level.

ASP.NET Core 8, EF Core 8 and SQLite, with a small UI for the catalogue, orders and the movement
log.

## Status

Working prototype: single node, no authentication, no tenant isolation. Read
[Not included](#not-included) before putting it anywhere public.

## Run

Requires the .NET 8 SDK.

```bash
dotnet run --project Ledgr.Api
```

- UI: <http://localhost:5196>
- API reference: <http://localhost:5196/swagger> (development only)

The first run applies migrations, creates `ledgr.db` next to the project and seeds five products.
Delete that file to start with an empty database.

## Test

```bash
dotnet test
```

Tests use real SQLite databases rather than the EF in-memory provider, so transactions, check
constraints and concurrency tokens are exercised. CI runs the build, `dotnet format
--verify-no-changes` and the suite with a 90% line-coverage floor on application code (migrations
and the composition root are excluded).

## How stock stays correct

1. `Product.QuantityOnHand` has a private setter. The only way to change it is `ApplyMovement`,
   which validates the movement type and sign, refuses negative stock, and appends the ledger row.
2. `ApplicationDbContext.SaveChanges` rejects any update or delete of an `InventoryMovement`.
3. A check constraint (`QuantityOnHand >= 0`) backs the same rule in the database.
4. `Product.Version` and `Order.Version` are optimistic concurrency tokens, bumped in
   `SaveChanges`. A losing write becomes `409 Stock conflict`; nothing is silently overwritten.
5. Order creation runs in a transaction, so a failure on the second line rolls back the first.

SQLite serialises writers, so the token is a correctness backstop rather than a throughput
feature. A multi-node deployment needs a server database and a retry policy.

## API

| Method | Path | Purpose |
| :--- | :--- | :--- |
| `GET` | `/api/products` | Catalogue page. `search`, `category`, `onlyLowStock`, `page`, `pageSize` |
| `GET` | `/api/products/{id}` | One product |
| `GET` | `/api/products/categories` | Distinct active categories |
| `POST` | `/api/products` | Create a product, optionally with opening stock |
| `PUT` | `/api/products/{id}` | Update name, description, category, price, reorder level, active flag |
| `DELETE` | `/api/products/{id}` | Deactivate a product (rows and history are kept) |
| `POST` | `/api/inventory/adjust` | Stock in, correction or write-off |
| `GET` | `/api/inventory/movements` | Movement log, newest first |
| `POST` | `/api/orders` | Place an order; accepts an `Idempotency-Key` header |
| `GET` | `/api/orders` | Order page |
| `GET` | `/api/orders/{id}` | One order with its lines |
| `POST` | `/api/orders/{id}/cancel` | Cancel and return the units to stock |
| `GET` | `/api/dashboard/kpis` | Counts, low-stock alerts and recent movements |
| `GET` | `/health` | Liveness plus a database probe |

List endpoints return `{ items, page, pageSize, totalCount, totalPages }`. `pageSize` is clamped to
100. Requests that change data are limited to 120 per minute per IP.

Errors use problem details:

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Insufficient stock",
  "status": 409,
  "detail": "Only 3 unit(s) of SKU-ANKR-737 are in stock; 5 were requested.",
  "instance": "/api/orders",
  "traceId": "0HN7A1B2C3D4E:00000001"
}
```

## Configuration

| Key | Default | Notes |
| :--- | :--- | :--- |
| `Database:ConnectionString` | `Data Source=ledgr.db` | SQLite connection string |
| `Database:MigrateOnStartup` | `true` | Set to `false` and run `dotnet ef database update` from a deploy step instead |
| `Database:Seed` | `false` (`true` in Development) | Inserts the sample catalogue when the database is empty |
| `Cors:AllowedOrigins` | none | Add origins only if a browser client is served from another host |

`dotnet ef` is pinned as a local tool: run `dotnet tool restore` before using it.

## Layout

```
Ledgr.Api/
  Controllers/     HTTP surface
  Services/        Business rules and transactions
  Models/          Product, Order, OrderItem, InventoryMovement
  Data/            DbContext, migrations, seeding
  Dtos/            Request and response contracts
  Middleware/      Problem details handler, security headers
  wwwroot/         UI (no bundler, no framework)
Ledgr.Tests/
  *ServiceTests.cs Domain behaviour against real SQLite
  Api/             End-to-end HTTP tests
```

## Not included

Deliberately left out rather than half-built:

- Authentication, authorisation and tenant isolation.
- Multiple warehouses or stock locations.
- Reservations, partial fulfilment and returns.
- Retries for transient database errors.
- Multi-currency and localisation; amounts are USD and timestamps are UTC.

## License

MIT. See [LICENSE](LICENSE).
