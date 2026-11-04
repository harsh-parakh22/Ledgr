# Product Requirements Document (PRD) — Ledgr

**Document Version:** 1.0.0  
**Status:** Approved  
**Author:** Harsh Parakh  
**Target Domain:** SaaS E-Commerce, Retail Inventory Management & Order Fulfillment  

---

## 1. Executive Summary
**Ledgr** is a cloud-native SaaS inventory and order orchestration engine designed to eliminate overselling, inventory discrepancies, and audit blind spots for modern multi-channel product sellers. 

Unlike traditional inventory systems that treat stock counts as plain mutable integers, Ledgr operates on an **immutable, double-entry inventory ledger model** backed by ACID database transactions. Every physical addition, reservation, deduction, write-off, or return creates an unalterable audit record, ensuring 100% stock reconciliation and transparency across high-concurrency order spikes.

---

## 2. Problem Statement
1. **Overselling & Race Conditions:** When concurrent checkout requests arrive simultaneously for items with low stock, standard un-isolated database reads lead to overselling (negative stock), creating fulfillment failures and customer friction.
2. **Untraceable Inventory Discrepancies:** Modifying stock counters directly (`stock = stock - 1`) removes operational accountability. Warehouse managers cannot identify when, why, or through which channel stock vanished or changed.
3. **Lack of Threshold Intelligence:** Small-to-medium sellers fail to reorder on time due to lack of dynamic reorder-point tracking, causing stockouts of high-velocity goods.

---

## 3. Target User Personas

| Persona | Role | Key Needs & Pain Points |
| :--- | :--- | :--- |
| **Warehouse Operations Lead** | Inventory Controller | Needs reliable physical stock counts, accurate write-off logs (damaged goods/shrinkage), and clear purchase order receiving entries. |
| **E-Commerce Store Owner** | Seller / Merchant | Needs real-time stock alerts to reorder from suppliers before stockouts occur, and zero overselling during promotional sales. |
| **Fulfillment Associate** | Order Picker / Packer | Needs fast order allocation, guaranteed stock reservation, and clear error messaging when inventory is depleted. |

---

## 4. Scope & Boundaries

### 4.1 In Scope (Phase 1 MVP)
- **Product Catalog Management:** SKU, Name, Category, Price, Stock on Hand, Reorder Level, Soft-delete lifecycle (`IsActive`).
- **Double-Entry Stock Ledger:** Immutable `InventoryMovement` log tracking deltas (`QuantityChange`), before/after snapshots, audit reasons, and document references.
- **Transactional Order Placement:** Atomic reservation and stock deduction using ACID transactions (`BeginTransactionAsync`).
- **Low-Stock Alerting Engine:** Dynamic detection of SKUs operating at or below `ReorderLevel`.
- **Operational Dashboard:** Real-time visibility into Total Inventory Valuation, SKU Health, and Recent Ledger Events.

### 4.2 Out of Scope (Future Roadmap)
- Multi-warehouse routing (Phase 2).
- Third-party courier tracking API integration (DHL, FedEx).
- Automated AI restock purchase-order generation (Phase 3).

---

## 5. Functional Requirements (FR)

### FR-1: Product Lifecycle
- **FR-1.1:** System MUST enforce unique SKUs (alphanumeric, case-insensitive).
- **FR-1.2:** System MUST validate that `UnitPrice` is greater than 0.
- **FR-1.3:** Deleting a product MUST perform a soft-deactivate (`IsActive = false`) to maintain referential integrity with historic orders and ledger transactions.

### FR-2: Immutable Stock Ledger
- **FR-2.1:** Stock counts on products MUST NOT be modified directly without an accompanying `InventoryMovement` audit record.
- **FR-2.2:** Each ledger entry MUST record: `ProductId`, `QuantityChange`, `PreviousQuantity`, `NewQuantity`, `MovementType`, `Reason`, `ReferenceId`, and UTC timestamp.
- **FR-2.3:** System MUST reject any adjustment that results in negative on-hand stock (`QuantityOnHand < 0`).

### FR-3: Order Processing & Concurrency
- **FR-3.1:** System MUST process order creation inside an ACID database transaction.
- **FR-3.2:** If any product in an order has insufficient stock (`RequestedQty > QuantityOnHand`), the entire order MUST fail and roll back with an `INSUFFICIENT_STOCK` error.
- **FR-3.3:** Upon successful order placement, stock MUST be immediately decremented, order line items recorded, and an `OrderFulfill` ledger entry appended.

---

## 6. Non-Functional Requirements (NFR)

- **Data Integrity & Consistency:** Zero tolerated stock drift. Transactions must be atomic and serializable/snapshot-isolated.
- **Performance:** Read operations (catalog, dashboard) response time $< 50\text{ ms}$; write operations (order checkout, ledger adjustment) $< 150\text{ ms}$.
- **Observability:** Structured logging via Serilog; standardized RFC 7807 `ProblemDetails` for all client errors.
- **Test Coverage:** Minimum 85% branch coverage on domain services, verified via strict Test-Driven Development (TDD).

