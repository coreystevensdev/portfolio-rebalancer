# ADR-003: PostgreSQL numeric with a per-column scale for financial values

**Status:** Accepted
**Date:** 2026-06-01
**Amended:** 2026-11-20, to describe the scales the schema actually uses. The original said `numeric(18,4)` everywhere, which was never true of the code and gave two sample lines that do not appear in `AppDbContext`.

## Context

Financial values in this domain include: share counts, cost basis per share, market values, portfolio weights, drift percentages, and rebalancing order quantities. All of them require exact decimal arithmetic; approximation errors compound across calculations.

Two storage options are common in .NET + EF Core applications:

1. `double` (C#) mapped to `double precision` (PostgreSQL): IEEE 754 floating-point, subject to rounding errors (e.g., `0.1 + 0.2 != 0.3` in floating-point).
2. `decimal` (C#) mapped to `numeric` (PostgreSQL): arbitrary-precision fixed-point, no rounding error within the defined precision.

## Decision

Every financial value in the `Domain/` layer is `decimal` in C#, and every one gets an explicit `HasColumnType` in `OnModelCreating` rather than an EF default. The scale is chosen per column, because a weight and a dollar amount do not need the same one:

```csharp
e.Property(p => p.DriftTolerancePct).HasColumnType("numeric(6,4)");
e.Property(a => a.TargetWeight).HasColumnType("numeric(8,6)");
e.Property(l => l.Shares).HasColumnType("numeric(18,6)");
e.Property(l => l.CostBasisPerShare).HasColumnType("numeric(18,4)");
e.Property(o => o.Shares).HasColumnType("numeric(18,6)");
e.Property(o => o.EstimatedValue).HasColumnType("numeric(18,2)");
```

Weights carry six decimal places because they are validated to sum to 1.0 within 0.001 and a basis point needs four to survive the comparison. Share counts carry six because fractional-share brokers quote that far. Money carries two or four: four where it is a per-share input that gets multiplied, two where it is a total that gets displayed.

`Holding.TotalShares` has no column type because it has no column. It is `Lots.Sum(l => l.Shares)` on the entity and `e.Ignore(h => h.TotalShares)` in the model, so the lots are the record and the total is always derived from them.

Rounding is not confined to one layer, and the scales differ on purpose. `DriftCalculator.Calculate` rounds `ActualWeight` to 6 to match the column, `DriftPct` to 4 because that is the precision the tolerance is expressed in, and `MarketValue` and the report total to 2 because they are dollar figures a person reads. The arithmetic in between stays unrounded `decimal`.

## Consequences

**Better:** Drift percentages, rebalancing order quantities, and cost basis calculations are exact. A holding of 10.0001 shares at $100.0001 does not round differently in application code vs. the database. The test suite (TestContainers + real PostgreSQL) can verify precision invariants that SQLite would silently corrupt.

**Worse:** `numeric` is slightly slower than `double precision` in PostgreSQL for arithmetic-heavy queries. Not relevant here: drift calculation is done in application code on portfolio-sized data (tens of positions), not in SQL aggregates over millions of rows.

**Worse:** five distinct scales is five chances to pick the wrong one, and a mismatch between a column's scale and the `Math.Round` that feeds it would be invisible until a value needed the digits. The pairing is only enforced by reading both files together, not by a test.

**Scale limits:** `numeric(18,2)` on `EstimatedValue` caps an order at roughly $9,999,999,999,999,999.99, and `numeric(8,6)` caps a target weight at 99.999999, well above the 1.0 the validator allows. Neither is a practical limit for this system.

**Rejected alternative:** `double` + `double precision`. Rejected because financial software with floating-point arithmetic errors is a known class of production bug (documented in literature: Goldberg 1991, "What Every Computer Scientist Should Know About Floating-Point Arithmetic"). The precision cost is negligible; the correctness benefit is non-negotiable.

**Rejected alternative:** one scale everywhere, which is what this document used to claim. `numeric(18,4)` on a target weight wastes ten digits to the left of the point it can never use and loses the two to the right that basis-point comparisons need.
