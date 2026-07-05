# ADR-003: PostgreSQL numeric(18,4) for all financial values

**Status:** Accepted
**Date:** 2026-06-01

## Context

Financial values in this domain include: share counts, cost basis per share, market values, portfolio weights, drift percentages, and rebalancing order quantities. All of them require exact decimal arithmetic; approximation errors compound across calculations.

Two storage options are common in .NET + EF Core applications:

1. `double` (C#) mapped to `double precision` (PostgreSQL): IEEE 754 floating-point, subject to rounding errors (e.g., `0.1 + 0.2 != 0.3` in floating-point).
2. `decimal` (C#) mapped to `numeric(18,4)` (PostgreSQL): arbitrary-precision fixed-point, no rounding error within the defined precision.

## Decision

All financial values in the `Domain/` layer use `decimal` in C#. EF Core column type annotations specify `numeric(18,4)` explicitly on every relevant property via `OnModelCreating`:

```csharp
builder.Property(h => h.TotalShares).HasColumnType("numeric(18,4)");
builder.Property(a => a.TargetWeight).HasColumnType("numeric(18,4)");
```

The drift calculation result (`DriftPct`, `ActualWeight`) uses `decimal` arithmetic throughout and rounds only at the display layer (`Math.Round(..., 4)` in `DriftCalculator.Calculate`).

## Consequences

**Better:** Drift percentages, rebalancing order quantities, and cost basis calculations are exact. A holding of 10.0001 shares at $100.0001 does not round differently in application code vs. the database. The test suite (TestContainers + real PostgreSQL) can verify precision invariants that SQLite would silently corrupt.

**Worse:** `numeric(18,4)` is slightly slower than `double precision` in PostgreSQL for arithmetic-heavy queries. Not relevant here: drift calculation is done in application code on portfolio-sized data (tens of positions), not in SQL aggregates over millions of rows.

**Scale limits:** `numeric(18,4)` supports up to 14 digits before the decimal point, or portfolio values up to approximately $99,999,999,999,999.9999. Sufficient for any realistic portfolio this system would manage.

**Rejected alternative:** `double` + `double precision`. Rejected because financial software with floating-point arithmetic errors is a known class of production bug (documented in literature: Goldberg 1991, "What Every Computer Scientist Should Know About Floating-Point Arithmetic"). The precision cost is negligible; the correctness benefit is non-negotiable.
