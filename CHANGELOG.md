# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Changed
- **Repository Structure & Conventions**: Reorganized repository into standard `src/`, `tests/` (`Unit/`, `Integration/`, `Support/`), and `scripts/` directories.
- **Contract & Domain Modularization**: Decomposed composite DTO and exception classes into single-responsibility types with aligned `Ledgr.Api.Contracts` namespace.
- **Engineering Hygiene & Containerization**: Added `.dockerignore`, `.gitattributes`, runtime directory permissions in Dockerfile, and unified CI workflow as `ci.yml`.
- **Documentation & Link Integrity**: Renamed and reorganized `/docs` folder into clear categories and fixed all referenced links.

---

## [0.2.0] - 2026-11-11

### Added
- **Transactional Order Placement Engine**: Implemented `OrderService` with ACID database transactions (`BeginTransactionAsync`), eliminating race conditions and preventing overselling across concurrent checkout flows.
- **Stock Movement Journal**: Implemented immutable `InventoryMovement` audit log with snapshot guarantees ($NewQuantity = PreviousQuantity + QuantityChange$).
- **Optimistic Concurrency Control**: Added integer `Version` concurrency tokens via `IsConcurrencyToken()` to protect against lost updates.
- **RFC 9457 Standard Error Handling**: Centralized `LedgrExceptionHandler` returning standardized `ProblemDetails` JSON responses with machine-readable error codes.
- **Interactive Web UI**: Responsive operator dashboard built in vanilla ES6 JavaScript and Bootstrap 5 with KPI metric cards, stock adjustment modals, and order placement.
- **Automated Test Suite**: 46 unit and integration tests using xUnit, FluentAssertions, and Moq achieving complete invariant coverage.
- **Automated CI/CD**: GitHub Actions workflow testing builds across Linux runners with cold restores.
- **Complete Architecture Documentation**: Product Requirements Document (PRD), Technical Architecture Design, Database Schema & ER diagrams, and API specifications.

---

## [0.1.0] - 2026-11-04

### Added
- Initial project scaffolding: Solution file, ASP.NET Core Web API, and xUnit test harness.
- Pre-development specification documents and repository setup.
