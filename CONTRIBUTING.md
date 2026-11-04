# Contributing Guidelines & Engineering Standards — Ledgr

Thank you for contributing to **Ledgr**! To maintain enterprise software quality aligned with MNC and product company standards, please adhere to these guidelines.

---

## 1. Branching Model

We follow a **Trunk-Based / Feature-Branch Workflow**:
- `main`: Production-ready, always building and passing tests.
- `feat/<feature-name>`: New features (e.g. `feat/multi-location-routing`).
- `test/<test-name>`: Test suite additions (e.g. `test/concurrency-order-race`).
- `fix/<bug-name>`: Bug fixes and patches.

---

## 2. Commit Message Standards (Conventional Commits)

Commit messages must follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>(<optional scope>): <description>

[optional body]
```

### Types:
- `feat`: A new user-facing or API feature
- `test`: Adding or refactoring tests (strict TDD commits)
- `fix`: A bug fix or invariant patch
- `docs`: Documentation updates or design diagrams
- `refactor`: Code changes that neither fix a bug nor add a feature
- `ci`: CI/CD workflow modifications

**Examples:**
- `test(orders): add unit test for concurrent stock reservation failure`
- `feat(inventory): implement double-entry stock movement ledger endpoint`
- `docs(arch): add mermaid sequence diagram for order allocation`

---

## 3. Strict TDD Workflow Rule

When adding a new feature or constraint:
1. **Commit 1 (RED):** Write the failing test in `Ledgr.Tests`.
2. **Commit 2 (GREEN):** Implement the minimal code in `Ledgr.Api` to pass the test.
3. **Commit 3 (REFACTOR):** Clean up, format, and optimize without breaking tests.

---

## 4. C# Code Formatting & Style
- Follow official Microsoft C# conventions.
- Use explicit types for domain records and DTOs (`record`).
- Prefer async/await all the way through (`Task<T>`, `CancellationToken`).
- Nullable reference types (`<Nullable>enable</Nullable>`) are enforced across all projects.

