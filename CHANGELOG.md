# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.2.0] - 2026-11-11

### Added
- **Transactional Order Placement Engine**: Implemented `OrderService` with ACID database transactions (`BeginTransactionAsync`), eliminating race conditions and preventing overselling across concurrent checkout flows.
- **Double-Entry Inventory Ledger**: Implemented immutable `InventoryMovement` audit log with balance equation guarantees ($Stock_{new} = Stock_{old} + \Delta$).
- **Optimistic Concurrency Control**: Added `RowVersion` concurrency tokens to protect against lost updates.
- **RFC 7807 Standard Error Handling**: Centralized `LedgrExceptionHandler` returning standardized `ProblemDetails` JSON responses with machine-readable error codes.
- **Interactive Web UI**: Responsive operator dashboard built in vanilla ES6 JavaScript and Bootstrap 5 with KPI metric cards, stock adjustment modals, and order placement.
- **Automated Test Suite**: 46 unit and integration tests using xUnit, FluentAssertions, and Moq achieving complete invariant coverage.
- **Automated CI/CD**: GitHub Actions workflow testing builds across Linux runners with cold restores.
- **Complete Architecture Documentation**: Product Requirements Document (PRD), Technical Architecture Design, Database Schema & ER diagrams, and API specifications.

---

## [0.1.0] - 2026-11-04

### Added
- Initial project scaffolding: Solution file, ASP.NET Core Web API, and xUnit test harness.
- Pre-development specification documents and repository setup.
