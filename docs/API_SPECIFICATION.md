# API Specification & Contracts — Ledgr

**Document Version:** 1.0.0  
**Status:** Approved  
**Author:** Harsh Parakh  
**Protocol:** HTTP/1.1 & HTTP/2 RESTful JSON  
**Error Standard:** RFC 7807 (Problem Details for HTTP APIs)  

---

## 1. RESTful Endpoints Catalog

### 1.1 Products API (`/api/Products`)

| Method | Endpoint | Description | Status Codes |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/Products` | Retrieve product catalog with optional query filters (`search`, `category`, `onlyLowStock`). | `200 OK` |
| `GET` | `/api/Products/{id}` | Retrieve specific product details by ID. | `200 OK`, `404 Not Found` |
| `POST` | `/api/Products` | Create a new product and optional opening stock ledger entry. | `201 Created`, `400 Bad Request`, `409 Conflict` |
| `PUT` | `/api/Products/{id}` | Update product details (pricing, reorder level, category). | `200 OK`, `404 Not Found` |
| `DELETE`| `/api/Products/{id}` | Soft-deactivate product (`IsActive = false`). | `204 No Content`, `404 Not Found` |

---

### 1.2 Inventory Ledger API (`/api/Inventory`)

| Method | Endpoint | Description | Status Codes |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/Inventory/adjust` | Record a stock adjustment (Restock, Cycle Count, Write-Off) into the ledger. | `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `GET` | `/api/Inventory/movements` | Retrieve chronological audit trail of stock movements. | `200 OK` |

#### Payload: `StockAdjustmentDto`
```json
{
  "productId": 1,
  "quantityChange": 25,
  "type": 1,
  "reason": "Received restock PO-4091 from supplier",
  "referenceId": "PO-4091"
}
```

---

### 1.3 Orders API (`/api/Orders`)

| Method | Endpoint | Description | Status Codes |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/Orders` | Place customer order with atomic stock reservation and ledger write. | `201 Created`, `400 Bad Request`, `409 Conflict` |
| `GET` | `/api/Orders` | Retrieve list of placed orders with line items. | `200 OK` |
| `GET` | `/api/Orders/{id}` | Retrieve order details by ID. | `200 OK`, `404 Not Found` |

#### Payload: `CreateOrderDto`
```json
{
  "customerName": "Acme Retailers Ltd",
  "customerEmail": "procurement@acme.com",
  "items": [
    {
      "productId": 1,
      "quantity": 2
    },
    {
      "productId": 3,
      "quantity": 1
    }
  ]
}
```

---

### 1.4 Dashboard KPI API (`/api/Dashboard`)

| Method | Endpoint | Description | Status Codes |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/Dashboard/kpis` | Real-time aggregated metrics (Total SKUs, Low Stock alerts, Order volume, Valuation). | `200 OK` |

---

## 2. Standardized Error Response (RFC 7807)

When a business rule is violated (e.g., ordering more items than available in stock), Ledgr returns an RFC 7807 compliant JSON object:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Insufficient Stock Available",
  "status": 409,
  "detail": "Insufficient stock for 'SKU-LOGI-MX3S'. Requested: 10, Available: 3.",
  "instance": "/api/Orders"
}
```

