# Technical Architecture & System Design Document (TDD / HLD) — Ledgr

**Document Version:** 1.0.0  
**Status:** Approved  
**Author:** Harsh Parakh  

---

## 1. Architectural Overview & Style

Ledgr is engineered following the **Clean Architecture / N-Tier Domain-Centric Pattern** with explicit separation between **Domain Entities**, **Business Services**, **Persistence (EF Core)**, and **API/Presentation Layers**.

```mermaid
graph TD
    UI["Frontend Client (Vanilla JS / Bootstrap / Fetch API)"] -->|HTTP / JSON| API["API Layer (ASP.NET Core Controllers)"]
    API -->|RFC 7807 Exception Handling| MW["Global Exception Middleware"]
    API -->|DTOs & Commands| Services["Service Layer (Domain Rules & Transactions)"]
    Services -->|Entity Framework Core 8| Data["Data Layer (ApplicationDbContext)"]
    Data -->|SQL Queries & Locks| DB[("SQL Server / Relational DB")]

    subgraph ServiceLayer ["Service Layer Scope"]
        IProductService["IProductService"]
        IInventoryService["IInventoryService"]
        IOrderService["IOrderService"]
        IDashboardService["IDashboardService"]
    end
```

---

## 2. Concurrency & Transaction Boundary Design

### 2.1 The Race Condition Threat (Double-Spend / Overselling)
In an e-commerce or retail fulfillment engine, concurrent checkout requests for the same SKU can cause overselling:

```
Thread A: Reads Stock = 1
Thread B: Reads Stock = 1
Thread A: Decrements (1 - 1 = 0) and commits.
Thread B: Decrements (1 - 1 = 0) and commits.
Result: 2 orders confirmed for 1 physical unit! (Stockout deficit)
```

### 2.2 Ledgr Concurrency Resolution
Ledgr enforces **Atomic Unit of Work with Database Transactions**:
1. Order creation initiates an explicit `IDbContextTransaction` via `BeginTransactionAsync()`.
2. The `IExecutionStrategy` pattern handles transient connection retries cleanly.
3. Inventory balances are evaluated and updated within the transaction boundary.
4. If validation fails for *any* line item, `RollbackAsync()` is invoked automatically, preventing partial allocations.

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Client / Webhook
    participant OrderCtrl as OrdersController
    participant OrderSvc as OrderService
    participant DbContext as ApplicationDbContext
    participant Database as Relational DB

    Customer->>OrderCtrl: POST /api/Orders (CustomerName, Items[])
    OrderCtrl->>OrderSvc: CreateOrderAsync(dto)
    OrderSvc->>DbContext: BeginTransactionAsync()
    OrderSvc->>DbContext: Fetch Products for all Items (Tracking)
    loop Validate & Allocate
        alt Requested Qty > QuantityOnHand
            OrderSvc-->>OrderCtrl: Throw InsufficientStockException
            OrderCtrl-->>Customer: 409 Conflict (RFC 7807 ProblemDetails)
            Note over OrderSvc,Database: Automatic Transaction Rollback
        else Stock Available
            OrderSvc->>DbContext: product.QuantityOnHand -= requestedQty
            OrderSvc->>DbContext: Add InventoryMovement(Delta = -requestedQty)
            OrderSvc->>DbContext: Add Order & OrderItems
        end
    end
    OrderSvc->>DbContext: SaveChangesAsync()
    OrderSvc->>DbContext: CommitTransactionAsync()
    OrderSvc-->>OrderCtrl: Return OrderDto
    OrderCtrl-->>Customer: 201 Created (Order Details + OrderNumber)
```

---

## 3. Component Architecture & Responsibilities

| Component | Namespace | Responsibility |
| :--- | :--- | :--- |
| **`Models`** | `Ledgr.Api.Models` | Pure domain entities (`Product`, `InventoryMovement`, `Order`, `OrderItem`). |
| **`DTOs`** | `Ledgr.Api.DTOs` | Immutable record structures (`record`) for strong typed API input/output validation. |
| **`Data`** | `Ledgr.Api.Data` | EF Core DbContext, entity fluent configurations, indexes, precision definitions, and database seeder. |
| **`Services`** | `Ledgr.Api.Services` | Core business logic, transaction isolation, stock validation, and ledger invariant enforcement. |
| **`Middleware`** | `Ledgr.Api.Middleware` | Centralized exception handling formatting standard RFC 7807 JSON responses. |
| **`Controllers`** | `Ledgr.Api.Controllers` | Thin REST controllers delegating to domain services. |

---

## 4. Error Handling Architecture (RFC 7807)
All domain errors inherit from `LedgrException` with explicit HTTP status codes:
- `ProductNotFoundException` $\rightarrow$ `404 Not Found`
- `InsufficientStockException` $\rightarrow$ `409 Conflict`
- `InactiveProductException` $\rightarrow$ `400 Bad Request`
- `DuplicateSkuException` $\rightarrow$ `409 Conflict`

