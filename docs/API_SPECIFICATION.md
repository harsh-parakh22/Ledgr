# API

Base path `/api`. JSON in, JSON out, camelCase. All timestamps are UTC.

List endpoints return:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalCount": 0,
  "totalPages": 0
}
```

`page` is 1-based. `pageSize` is clamped to 1..100.

## Products

### `GET /api/products`

| Query | Type | Default | Notes |
| :--- | :--- | :--- | :--- |
| `search` | string | – | Matches name or SKU, case-insensitive; `%` and `_` are literal |
| `category` | string | – | Exact match |
| `onlyLowStock` | bool | `false` | `QuantityOnHand <= ReorderLevel` |
| `page` | int | `1` | |
| `pageSize` | int | `25` | Maximum 100 |

Inactive products are never returned.

### `POST /api/products`

```json
{
  "sku": "SKU-NEW-01",
  "name": "Cable organiser",
  "description": null,
  "category": "Accessories",
  "unitPrice": 12.5,
  "initialQuantity": 20,
  "reorderLevel": 5
}
```

`201 Created` with the product. A non-zero `initialQuantity` also writes a `StockIn` movement.
Duplicate SKU: `409`. Validation failure: `400`.

### `PUT /api/products/{id}`

Full update of the editable fields: `name`, `description`, `category`, `unitPrice`,
`reorderLevel`, `isActive`. SKU and stock are not editable here.

### `DELETE /api/products/{id}`

Deactivates the product and returns `204`. The row, its orders and its movements are kept.

### `GET /api/products/categories`

Distinct categories of active products, sorted.

## Inventory

### `POST /api/inventory/adjust`

```json
{
  "productId": 1,
  "quantityChange": -2,
  "type": 4,
  "reason": "Damaged in transit",
  "referenceId": "CLAIM-14"
}
```

`type` is one of `1` stock in, `3` correction, `4` write-off. The two order types (`2` and `5`) are
written by the order pipeline and are rejected here.

Rules: the change must be non-zero; stock in and cancellation must be positive; order and write-off
must be negative; corrections accept either sign. Going below zero returns `409`.

### `GET /api/inventory/movements`

Optional `productId`, `page`, `pageSize`. Newest first.

## Orders

### `POST /api/orders`

Optional header: `Idempotency-Key` (64 characters max).

```json
{
  "customerName": "Northwind Traders",
  "customerEmail": "ops@northwind.test",
  "items": [
    { "productId": 1, "quantity": 2 },
    { "productId": 3, "quantity": 1 }
  ]
}
```

- `201 Created` with the order and its lines, plus a `Location` header.
- `200 OK` with the original order when the idempotency key has been seen before.
- `409` when stock is insufficient, a product is inactive, or stock changed under the request.
- `400` for an empty or malformed body.
- An unknown product id returns `404`, even when the rest of the body is valid.

Duplicate lines for the same product are merged. Total quantity per product is limited to 10,000 and
50 lines per order.

Stock is deducted immediately, and a matching `OrderFulfill` movement is written per line.

### `GET /api/orders` / `GET /api/orders/{id}`

Order list and order detail, both with lines.

### `POST /api/orders/{id}/cancel`

Returns the units to stock with one `OrderCancel` movement per line, sets `status` to `Cancelled`
and `cancelledAtUtc`. Calling it twice is safe: the second call returns the order unchanged and does
not restock again.

## Dashboard

### `GET /api/dashboard/kpis`

```json
{
  "activeProducts": 5,
  "lowStockCount": 2,
  "openOrders": 1,
  "cancelledOrders": 0,
  "inventoryValue": 21047.54,
  "lowStockAlerts": [],
  "recentMovements": []
}
```

`inventoryValue` is `sum(unitPrice * quantityOnHand)` over active products. `lowStockAlerts` is
capped at 20; `recentMovements` at 8.

## Errors

Every error is RFC 9457 problem details:

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

| Status | When |
| :--- | :--- |
| `400` | Validation failure, malformed body, rejected movement type |
| `404` | Product or order does not exist |
| `409` | Insufficient stock, inactive product, duplicate SKU, concurrent stock change |
| `429` | More than 120 write requests per minute from one IP |

`500` responses carry a generic message; the exception and trace id go to the log.

## Health

`GET /health` returns `{"status":"ok"}` after a database round trip, or `503` when the database is
unreachable.
