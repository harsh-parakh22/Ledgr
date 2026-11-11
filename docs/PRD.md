# Scope

## Problem

A seller with one warehouse can confirm two orders for the same last unit, and cannot explain why
the stock count changed three weeks ago. Both failures are cheap to prevent at the data layer and
expensive to clean up afterwards.

## Users

- **Warehouse operator** — receives deliveries, writes off damaged stock, checks why a count moved.
- **Seller** — watches low-stock items, places and cancels orders.
- **Integrator** — calls the API from a storefront or an internal tool.

## In scope, implemented

- Product catalogue: SKU, name, category, description, price, reorder level, soft delete.
- Stock ledger: one append-only row per change, with before/after quantities, reason and reference.
- Stock adjustments: delivery, count correction, write-off.
- Order placement: transactional deduction, duplicate-line merging, idempotent retries.
- Order cancellation: compensating movements, safe to call twice.
- Low-stock detection and a dashboard summary.
- Web UI for the catalogue, orders and movement log.

## Functional requirements

| Id | Requirement | Where |
| :--- | :--- | :--- |
| FR-1 | SKUs are unique and case-insensitive | `Products.Sku` unique index; normalised in `ProductService` |
| FR-2 | Price is greater than zero | `CreateProductDto` range validation |
| FR-3 | Deleting a product deactivates it and keeps history | `ProductService.DeactivateAsync` |
| FR-4 | Every stock change writes a ledger row in the same operation | `Product.ApplyMovement` |
| FR-5 | Ledger rows cannot be changed or deleted | `ApplicationDbContext.PrepareChanges` |
| FR-6 | Stock can never go negative | domain check plus `CK_Products_QuantityOnHand` |
| FR-7 | An order is all-or-nothing | `OrderService.CreateCoreAsync` transaction |
| FR-8 | Retrying an order with the same key does not duplicate it | `Orders.IdempotencyKey` unique index |
| FR-9 | Cancelling restores stock and is idempotent | `OrderService.CancelAsync` |
| FR-10 | Concurrent writes cannot silently overwrite each other | `Version` concurrency tokens |

## Non-functional

- Responses are problem details; 5xx bodies never contain exception text.
- Writes are rate limited to 120 per minute per IP.
- Reads are paginated, with a hard ceiling of 100 rows per page.
- The UI ships no third-party JavaScript beyond a local copy of Bootstrap.

## Out of scope

- Authentication, authorisation and tenants.
- Multiple warehouses and transfers between them.
- Reservations, partial fulfilment, returns and refunds.
- Purchase orders and supplier records.
- Multi-currency, taxes and invoicing.

These are product decisions, not omissions: each changes the data model, and building a thin
version now would only have to be replaced.
