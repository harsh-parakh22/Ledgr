# Architecture

## Shape

One web project and one test project. Controllers call services, services use `ApplicationDbContext`
directly, and EF Core talks to SQLite. There is no repository layer: at this size it would only add
a pass-through.

| Folder | Responsibility |
| :--- | :--- |
| `Controllers` | Bind input, call one service method, return a status code. No business rules. |
| `Services` | Transactions, validation, mapping to DTOs. |
| `Models` | Entities and the ledger rules. |
| `Data` | `DbContext`, migrations, seeding, design-time factory. |
| `Dtos` | Record types for requests and responses; entities never leave the process. |
| `Middleware` | Problem details, security headers. |

## Request flow: placing an order

1. `[ApiController]` validates the body. Invalid requests never reach the service.
2. `OrderService` normalises the idempotency key, then returns the existing order if the key was
   already used.
3. Duplicate product lines are merged; the original line order is kept.
4. A transaction is opened and the referenced products are loaded.
5. Unknown or inactive products are rejected before any stock changes.
6. Each line calls `Product.ApplyMovement`, which validates the delta, appends the movement and
   updates the quantity in one step.
7. `SaveChanges`, then commit. A concurrency failure rolls back and is reported as `409`.

## Concurrency

A transaction gives atomicity, not mutual exclusion. Two requests can both read stock of 5, both
pass the check, and both write 4. The second write would silently erase the first.

The guard is an optimistic concurrency token:

- `Product.Version` and `Order.Version` are configured with `IsConcurrencyToken()`.
- `ApplicationDbContext` increments the version of every modified `Product` or `Order` in
  `SaveChanges`, so no new write path can forget to participate.
- EF adds the original version to the `UPDATE` predicate. If another transaction got there first,
  zero rows match, EF raises `DbUpdateConcurrencyException`, and the service turns it into
  `StockConflictException` (HTTP 409).

The database also enforces `QuantityOnHand >= 0`, so a bug that bypasses the service still cannot
produce negative stock.

## Ledger invariants

Every state change must satisfy:

1. `NewQuantity = PreviousQuantity + QuantityChange`
2. `Product.QuantityOnHand = sum of that product's movements`
3. `QuantityOnHand >= 0` at all times

`InventoryMovement.Create` computes the snapshots from the product it is given, so the first two
cannot drift. Movements are append-only: `SaveChanges` throws if one is modified or deleted.
Reversals are new rows (`OrderCancel`), never edits.

## Why SQLite

The application needs a relational database with transactions and check constraints, not a
particular vendor. SQLite keeps the repository runnable with one command and lets the tests use the
same provider as the application, which is what the previous in-memory setup could not do.

The cost is that writers are serialised, and decimal values are stored as text. Two consequences
are visible in the code: totals are summed in memory to stay exact, and a multi-node deployment
would need a server database plus a retry policy.

## Trade-offs

- **No repository abstraction.** Services depend on EF Core. A second data source would justify
  one; a single one does not.
- **Domain exceptions in `Models`.** `InventoryMovement` throws `InsufficientStockException`
  directly. With one project, mapping exceptions at the edge is simpler than a result type on
  every call.
- **No API versioning yet.** One consumer, one deployment. Add a version prefix before external
  clients appear.
- **UI without a build step.** Plain ES modules and Bootstrap from `wwwroot`. A framework would be
  more code, not less, for three screens.
