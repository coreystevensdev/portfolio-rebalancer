# ADR-001: MediatR vertical slices over traditional controller classes

**Status:** Accepted
**Date:** 2026-06-01

## Context

The API exposes five operations across two domains (portfolios and rebalancing). The alternative approach is to organize code by technical layer: one `PortfoliosController` and one `RebalancingController`, each injecting service classes that in turn inject repositories.

Three-layer architecture (controller -> service -> repository) is familiar and well-documented, but it creates pressure to share services across operations, leading to fat service classes where every method has a different dependency footprint. In a financial domain where each operation has distinct authorization rules, validation, and side effects, this sharing is usually accidental coupling rather than genuine code reuse.

## Decision

Use MediatR 12 to implement each API operation as a self-contained vertical slice: one command or query record, one handler class, collocated in a `Features/` folder organized by business capability rather than technical layer.

- `POST /portfolios` -> `CreatePortfolioCommand` + `CreatePortfolioCommandHandler`
- `GET /portfolios/{id}` -> `GetPortfolioQuery` + `GetPortfolioQueryHandler`
- `POST /portfolios/{id}/holdings` -> `AddHoldingCommand` + `AddHoldingCommandHandler`
- `POST /portfolios/{id}/drift` -> `GetDriftQuery` + `GetDriftQueryHandler`
- `POST /portfolios/{id}/rebalance` -> `RebalanceCommand` + `RebalanceCommandHandler`

Each handler is responsible for authorization (ownership check), validation, domain logic invocation, and persistence. There is no shared service layer.

## Consequences

**Better:** Each operation can be understood and modified in isolation. A developer reading `RebalanceCommandHandler` does not need to understand the portfolio creation flow to make a change. Adding a new operation does not risk breaking existing ones. The command/query distinction (CQRS at the handler level) makes read vs. write paths explicit.

**Worse:** More files than a classic controller approach. Developers unfamiliar with MediatR need to learn where the dispatch call in `Program.cs` connects to handlers. Cross-cutting concerns (like the ownership check) are duplicated across handlers rather than extracted to a base class; this is acceptable here because each check is one line and extracting it would hide a security-critical invariant.

**Rejected alternative:** A shared `PortfolioService` injected into both portfolio and rebalancing controllers. Rejected because drift calculation, rebalancing, and portfolio CRUD have no shared state and combining them in one service would create an artificially large class with no cohesion benefit.
