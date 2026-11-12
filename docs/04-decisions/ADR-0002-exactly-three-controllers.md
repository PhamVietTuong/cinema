# ADR-0002: Exactly three API controllers, enforced by a test

- **Date:** 2026-10-09 (consolidation merged by `52613ae`; guard test added by `30afff5` the same day). Author to confirm that this is the decision date.
- **Status:** Proposed (draft reconstructed from the repository, not yet confirmed by the author)

## Context

- Before 2026-10-09 the host had further controllers for the staff tools. `14246b5` says "With Gate/BoxOffice/Operations/Workforce/StaffReport merged into CinemaController, there is no longer a standalone 'Staff' API group", and a series of commits (for example `74de767`, `25f1e14`) merged them into `CinemaController` on the branch `feature/single-cinema-controller`.
- After the merge, `9bec328` removed the separate staff Swagger document and `beb948c` regenerated the NSwag clients.
- `AGENTS.md` now says: only `CinemaController`, `IdentityController` and `PaymentController` may exist, and new endpoints go into `CinemaController` under a `#region <Area>`.
- `ControllerArchitectureTests.OnlyThreeControllersExist` and `EveryControllerIsInItsOwnSwaggerGroup` (`cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost.Tests/ControllerArchitectureTests.cs`) fail the build if a fourth controller appears or a controller leaves its Swagger group (`cinema`, `identity`, `payment`).

## Decision

The API surface is three controllers, one per Swagger group and one NSwag client each. Staff, gate, box-office, operations and reporting endpoints live in `CinemaController` in `#region` blocks, gated by role constants such as `RoleNames.GateKeepers` and `RoleNames.Reporting`. Splitting `CinemaController` into partial files is also disallowed by `AGENTS.md`.

Author to confirm: why the staff controllers were merged (for example one generated client for the staff app, fewer Swagger documents, or simpler routing).

## Consequences

- `CinemaController.cs` is a very large file (86 admin-gated actions plus the role-composite regions, `docs/03-technical/architecture.md` section 5), with many `#region` blocks (the first is at `CinemaController.cs:369`).
- A single authorization-coverage test can check every action (`ControllerAuthorizationCoverageTests`).
- The generated clients stay at three (`cinema`, `identity`, `payment`) and the frontend apps share the merged `CinemaServiceAgent` (`4bc7c3f`).
- Author to confirm: whether a very large controller is an accepted cost or a known debt.

## Alternatives considered

- **One controller per area (the layout before 2026-10-09).** Separate staff controllers existed (Gate, BoxOffice, Operations, Workforce, StaffReport) and a standalone `staff` Swagger group with its own generated client. They were merged by `74de767`, `25f1e14` and the commits around them (PR `52613ae`) and the group was removed in `9bec328` and `14246b5`.
- **Splitting `CinemaController` into partial files.** Rejected by `AGENTS.md` ("never ... split `CinemaController` into partial files"). Author to confirm: why partial files were ruled out.
