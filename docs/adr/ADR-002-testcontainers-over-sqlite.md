# ADR-002: TestContainers for integration tests instead of SQLite

**Status:** Accepted
**Date:** 2026-06-01
**Amended:** 2026-11-21. The container lifecycle is per test case, not per class, and two of the four PostgreSQL behaviours this claimed to exercise (`@>`, a `ticker` index) are not in the schema. The decision is unchanged.

## Context

Integration tests that exercise EF Core queries need a database. Two common approaches are:

1. Use SQLite in-memory mode, which EF Core supports via `UseSqlite("DataSource=:memory:")`.
2. Use a real PostgreSQL instance via TestContainers, which spins up a Docker container per test session.

## Decision

Use `Testcontainers.PostgreSql` to run a real PostgreSQL 16 instance for integration tests.

`PortfolioEndpointTests` implements `IAsyncLifetime` directly and starts its own `PostgreSqlContainer`. xunit constructs a new instance per test case, so `InitializeAsync` runs once per test rather than once per class: 9 containers and 9 migration runs today, which is the cost being accepted here. There is only one integration test class today, so a shared collection fixture has not been needed yet; if a second integration test class is added, extract a shared `IAsyncLifetime` fixture at that point instead of duplicating the container-startup logic.

## Consequences

**Better:** Queries are tested against the actual database engine used in production. PostgreSQL-specific behavior is exercised in tests: the `numeric(18,4)` column type on `CostBasisPerShare`, case-sensitivity in text comparisons, and the `UserId` index every query filters on. Migrations are validated on every test run, not just in CI.

**Worse:** Tests take longer to set up (container pull on first run: ~30 seconds; subsequent runs use Docker layer cache). Requires Docker to be running locally. Not a concern in CI because the GitHub Actions runner has Docker available.

**Rejected alternative:** SQLite in-memory. Rejected because SQLite does not support `numeric(18,4)` (it coerces to `REAL`, which uses IEEE 754 double-precision floating point). That coercion would cause the ADR-003 financial precision invariant to silently pass in tests while failing in production. A test suite that cannot detect this class of bug provides false confidence.
