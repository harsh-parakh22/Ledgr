# Testing

## Approach

Tests run against real SQLite databases, not the EF Core in-memory provider. That matters: the
in-memory provider ignores transactions, so a rollback test written against it proves nothing.
SQLite is also the provider the application uses, so query translation is exercised too.

- `TestDatabase` opens one in-memory SQLite connection, creates the schema with `EnsureCreated` and
  hands out contexts that share it. Disposing it drops the database.
- Each test gets a fresh instance, so tests do not share state and can run in parallel.
- API tests use `WebApplicationFactory<Program>` with the same provider and a per-factory database.

No mocking framework is used. The units under test are the services and the HTTP surface; both can
be exercised with real dependencies at this size.

## What is covered

| Area | Examples |
| :--- | :--- |
| Ledger arithmetic | opening stock writes one movement; zero opening stock writes none |
| Movement rules | zero delta, wrong sign for write-off, order types rejected on the manual endpoint |
| Insufficient stock | rejected and stock unchanged |
| Transactions | a failing second line leaves the first line's stock untouched |
| Concurrency | a stale context gets `StockConflictException`, not an overwrite |
| Idempotency | replaying a key returns the same order and deducts once |
| Cancellation | stock restored, second call does not restock again |
| Order mapping | `POST /api/orders` returns real product names and SKUs |
| Validation | empty body fields produce field-level problem details |
| Pagination | page size capped at 100, totals and page counts correct |
| Search | wildcards in user input are treated as literal text |
| Seeding | five products with opening movements; second run is a no-op |
| HTTP surface | status codes, `application/problem+json`, security headers, health probe |

## What is not covered

- **True write parallelism.** SQLite serialises writers, so the concurrency test forces a conflict
  with a stale context rather than two simultaneous requests. The token itself is what is tested.
- **Rate limiting.** The limit is configuration; a test would mostly assert the framework works.
- **The UI.** Checked manually in a browser and by loading the real page in jsdom during the
  rewrite; there is no browser test in CI.
- **Retry behaviour.** There is none; see the note about transient faults in the README.

## Running

```bash
dotnet test
```

Coverage, with the same exclusions as CI:

```bash
dotnet test /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=total
```

`Ledgr.Tests.csproj` excludes generated migrations, the design-time factory and `Program` from the
measurement. Those are either generated or plumbing; including them would inflate the number
without testing behaviour.

CI additionally runs `dotnet format --verify-no-changes` over the solution (migrations excluded).

## Conventions

- Name tests `Method_Scenario_Expectation`, for example
  `CreateAsync_WhenASecondLineHasNoStock_RollsBackTheFirst`.
- One behaviour per test; assert on observable state, not on call counts.
- Do not assert on exception messages that are likely to be reworded; assert on the exception type
  or a stable fragment.
