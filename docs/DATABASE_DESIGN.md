# Database Design & Schema Specification — Ledgr

**Document Version:** 1.0.0  
**Status:** Approved  
**Author:** Harsh Parakh  

---

## 1. Entity-Relationship (ER) Diagram

```mermaid
erDiagram
    PRODUCT ||--o{ INVENTORY_MOVEMENT : "audited by"
    PRODUCT ||--o{ ORDER_ITEM : "ordered in"
    ORDER ||--|{ ORDER_ITEM : "contains"

    PRODUCT {
        int Id PK
        string Sku UK "Indexed, Not Null"
        string Name "Not Null"
        string Description "Nullable"
        string Category "Indexed, Not Null"
        decimal UnitPrice "Precision(18,2)"
        int QuantityOnHand "Non-negative"
        int ReorderLevel "Default 5"
        bool IsActive "Default true"
        datetime CreatedAtUtc "Not Null"
        datetime UpdatedAtUtc "Nullable"
    }

    INVENTORY_MOVEMENT {
        int Id PK
        int ProductId FK "Indexed, Restrict Delete"
        int QuantityChange "Signed Integer (+/-)"
        int PreviousQuantity "Snapshot"
        int NewQuantity "Snapshot"
        int Type "Enum: 1=StockIn, 2=OrderFulfill, 3=Adjustment, 4=WriteOff"
        string Reason "Not Null"
        string ReferenceId "Nullable (Order# or PO#)"
        datetime CreatedAtUtc "Indexed, Not Null"
    }

    ORDER {
        int Id PK
        string OrderNumber UK "Indexed, Not Null"
        string CustomerName "Not Null"
        string CustomerEmail "Nullable"
        int Status "Enum: 1=Pending, 2=Confirmed, 3=Cancelled"
        decimal TotalAmount "Precision(18,2)"
        datetime CreatedAtUtc "Indexed, Not Null"
    }

    ORDER_ITEM {
        int Id PK
        int OrderId FK "Cascade Delete"
        int ProductId FK "Restrict Delete"
        int Quantity "Positive Integer >= 1"
        decimal UnitPrice "Precision(18,2)"
    }
```

---

## 2. Mathematical Integrity Invariants of the Stock Ledger

The double-entry ledger enforces strict physical and mathematical invariants across every state transition:

1. **Balance Equation Invariant:**
   $$\text{NewQuantity} = \text{PreviousQuantity} + \text{QuantityChange}$$
2. **Current Stock Equation:**
   $$\text{Product.QuantityOnHand} = \sum_{i=1}^{N} \text{Movement}_i.\text{QuantityChange}$$
   *(The current stock on hand is mathematically proven by the sum of all historical movements for that product).*
3. **Non-Negativity Constraint:**
   $$\text{Product.QuantityOnHand} \ge 0 \quad \forall \text{ operations}$$
   *(Any transaction attempting to drive on-hand stock below 0 is aborted and rolled back).*

---

## 3. Indexing Strategy & Performance

| Table | Index Columns | Index Type | Reason |
| :--- | :--- | :--- | :--- |
| `Products` | `Sku` | Unique B-Tree | Fast $O(1)$ lookups and duplicate prevention across channels. |
| `Products` | `Category`, `IsActive` | Composite | Fast filtering for storefront catalog & active inventory. |
| `InventoryMovements`| `ProductId`, `CreatedAtUtc` | Composite | High-speed audit logs retrieval per product in chronological order. |
| `Orders` | `OrderNumber` | Unique B-Tree | Guaranteed idempotency and fast order retrieval. |
| `Orders` | `CreatedAtUtc` | B-Tree | High-speed range queries for revenue dashboards. |

