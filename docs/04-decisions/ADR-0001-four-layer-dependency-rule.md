# ADR-0001: Four numbered backend layers, dependencies only point downward

- **Date:** Author to confirm the decision date. First evidence in git: `bdc6ab4` (2026-05-24, "backend init"), which creates the `1-Service`, `2-Business`, `3-Data`, `4-Foundation` project folders.
- **Status:** Proposed (draft reconstructed from the repository, not yet confirmed by the author)

## Context

- The backend (`cinemabe/Cinema.sln`) is split into four numbered folders: `1-Service` (Web API host and generated clients), `2-Business` (managers, DTOs, contracts), `3-Data` (entities, stores, EF Core), `4-Foundation` (logging).
- `AGENTS.md` states the rule: never reference a higher-numbered layer from a lower one; DTOs stay in `Cinema.Business.DTO`, entities in `Cinema.Data.Entities`.
- One narrow exception exists in the project files: `Cinema.Data` references `Cinema.Business.Contracts` (`cinemabe/Cinema/3-Data/Cinema.Data/Cinema.Data.csproj:5`) so that layer 3 can implement `ITokenService` (`JwtTokenService`). `docs/03-technical/architecture.md` sections 1 and 2 record it.
- A search of the test projects on 2026-11-12 found no test that asserts the direction of project references. It is held in place by the `ProjectReference` entries themselves and by review.

## Decision

The dependency direction is `1-Service` to `2-Business` to `3-Data` to `4-Foundation`, and a lower layer never references a higher one. Business code reaches storage only through the store interfaces in `Cinema.Data.Contracts`, and the host composes everything in `Program.cs`.

Author to confirm: why this layering was chosen (for example testability with mocked stores, as the Moq-based unit tests suggest, or a course requirement). The repository does not record the reason.

## Consequences

- Managers are unit-tested with mocked stores and no database (`docs/05-process/testing.md`; the CI comment says "no database required").
- The `Cinema.Data` to `Cinema.Business.Contracts` reference is a deliberate hole in the strict rule. Adding another upward reference would compile without any test failing.
- Author to confirm: whether an architecture test should be added for the project-reference direction, as exists for the controller count (ADR-0002).
