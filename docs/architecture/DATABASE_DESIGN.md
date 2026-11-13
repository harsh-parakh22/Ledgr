# Data model

SQLite through EF Core. The types below are the .NET types; SQLite stores `decimal` and
`DateTime` as text and `bool` as an integer.

## Products

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | int | Primary key |
| `Sku` | string(50) | Required, unique, stored upper-case |
| `Name` | string(150) | Required |
| `Description` | string(500)? | Nullable |
| `Category` | string(80) | Required |
| `UnitPrice` | decimal(18,2) | Required, greater than zero |
| `QuantityOnHand` | int | `CHECK (QuantityOnHand >= 0)`; private setter |
| `ReorderLevel` | int | Low-stock threshold |
| `IsActive` | bool | Soft delete; inactive products cannot be ordered |
| `Version` | int | Optimistic concurrency token |
| `CreatedAtUtc` / `UpdatedAtUtc` | DateTime / DateTime? | UTC |

Indexes: unique `Sku`; composite `(Category, IsActive)` for the catalogue filters.

## InventoryMovements

The ledger. Append-only: `SaveChanges` throws if a row is modified or deleted.

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | int | Primary key |
| `ProductId` | int | FK to `Products`, delete restricted |
| `QuantityChange` | int | Non-zero; sign depends on type |
| `PreviousQuantity` | int | Snapshot before the change |
| `NewQuantity` | int | Snapshot after the change |
| `Type` | int | 1 stock in, 2 order, 3 correction, 4 write-off, 5 cancellation |
| `Reason` | string(250) | Required |
| `ReferenceId` | string(64)? | Order number or external document |
| `CreatedAtUtc` | DateTime | Indexed with `ProductId` |

Indexes: composite `(ProductId, CreatedAtUtc)`, plus the foreign key index.

## Orders

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | int | Primary key |
| `OrderNumber` | string(50) | Unique, `ORD-yyyyMMdd-<16 hex>` |
| `IdempotencyKey` | string(64) | Unique; client header or a generated value |
| `CustomerName` | string(120) | Required |
| `CustomerEmail` | string(150)? | Optional |
| `Status` | int | 1 confirmed, 2 cancelled |
| `TotalAmount` | decimal(18,2) | Sum of line totals at the time of sale |
| `Version` | int | Optimistic concurrency token |
| `CreatedAtUtc` / `CancelledAtUtc` | DateTime / DateTime? | UTC |

Indexes: unique `OrderNumber`, unique `IdempotencyKey`, `CreatedAtUtc` for the order list.

## OrderItems

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | int | Primary key |
| `OrderId` | int | FK to `Orders`, cascade delete |
| `ProductId` | int | FK to `Products`, restrict delete |
| `Quantity` | int | Greater than zero |
| `UnitPrice` | decimal(18,2) | Price captured at sale time |

`LineTotal` is computed (`Quantity * UnitPrice`) and not stored.

## Invariants

1. `NewQuantity = PreviousQuantity + QuantityChange` for every movement.
2. `Products.QuantityOnHand` equals the sum of that product's movements.
3. `QuantityOnHand` never goes below zero (validated in code and enforced by the check constraint).

## Notes

- Money is `decimal(18,2)`. SQLite stores it as text, so SQL aggregates over decimals are avoided;
  the dashboard reads two columns and sums in memory to keep the figure exact.
- Timestamps are written as UTC and converted back to `DateTimeKind.Utc` by a value converter, so
  API responses carry `Z` and clients can format them in local time.
- Deletes are soft: products are deactivated, orders are cancelled. Nothing in the ledger is
  removed.
