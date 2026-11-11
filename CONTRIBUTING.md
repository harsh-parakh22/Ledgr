# Contributing

## Setup

```bash
dotnet tool restore
dotnet build
```

Requires the .NET 8 SDK. The pinned version is in `global.json`; any 8.0.1xx or newer feature band
is accepted.

## Branches

- `main` — releasable. Every commit on it builds and passes the suite.
- `develop` — integration branch.
- `feat/<name>`, `fix/<name>`, `test/<name>` — work in progress.

## Commits

Conventional Commits, one logical change per commit:

```
feat(orders): reject duplicate lines with a merged quantity
fix(inventory): keep stock unchanged when the write-off is rejected
test(orders): cover a failing second line rolling back the first
docs(api): document the idempotency header
```

## Before opening a pull request

```bash
dotnet build -c Release
dotnet test
dotnet format --verify-no-changes --exclude Ledgr.Api/Data/Migrations
```

CI runs the same three steps, plus a 90% line-coverage floor on application code.

## Code

- File-scoped namespaces, nullable enabled, warnings are errors.
- One type per file, except for small related records such as the DTOs.
- Comments explain a decision, not a statement. `// increment the counter` is noise;
  `// bumped here so a new write path cannot forget the concurrency token` is not.
- New business rule: write the failing test first, then the smallest change that makes it pass.
- Do not bypass `Product.ApplyMovement` to change stock. If a new flow needs one, add a method
  there.

## Adding a migration

```bash
dotnet ef migrations add <Name> --project Ledgr.Api --output-dir Data/Migrations
```

Review the generated SQL before committing it. EF's design-time factory uses `ledgr.design.db`, so
the command never touches your working database.
