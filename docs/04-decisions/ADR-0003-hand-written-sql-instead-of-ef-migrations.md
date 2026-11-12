# ADR-0003: Schema changes are hand-written SQL scripts, not EF Core migrations

- **Date:** 2026-08-30 (`7065d9c` removes the last migration). Author to confirm that this is the decision date.
- **Status:** Proposed (draft reconstructed from the repository, not yet confirmed by the author)

## Context

- On 2026-07-26 `fc55b29` adopted EF Core migrations with an `InitialBaseline`, because the two hand-written scripts "had to be kept in sync by hand" (its words). Both scripts were made to stamp `__EFMigrationsHistory` with the baseline.
- On 2026-08-30 `7065d9c` dropped the `RoomTypeThreeDSupport` migration and moved its two columns and data fixes into `create_db.sql`, `upgrade_db.sql` and `insert_db.sql`. The folder `cinemabe/Cinema/3-Data/Cinema.Data/Migrations/` now holds only the baseline and the model snapshot.
- `AGENTS.md` states the current rule: schema changes go by hand into `cinemabe/sql/create_db.sql` and `upgrade_db.sql`, applied directly to the dev DB; never `dotnet ef migrations add`.
- `create_db.sql` drops and recreates tables; `upgrade_db.sql` is written to be idempotent (`IF NOT EXISTS` guards). `docker-compose.yml` runs create, upgrade and seed only on an empty database, and a marker table `dbo.__DbInit` stops a restart from wiping data.

## Decision

The SQL scripts in `cinemabe/sql/` are the source of truth for the schema. The Fluent configurations in `Cinema.Data/Configurations/` describe the same model and are kept in sync by hand. New `DateTime` columns become SQL `datetime` through `CinemaContext.OnModelCreating`, not by per-column mapping.

Author to confirm: why the project moved away from migrations after adopting them (the commit message of `7065d9c` explains how, not why).

## Consequences

- `7065d9c` itself records a cost: the EF model snapshot no longer describes the two columns the scripts created, so a future `migrations add` would emit an `AddColumn` for columns that already exist and that diff would need deleting by hand.
- Every schema change touches two scripts plus the entity and its configuration, and must be applied to the dev database by hand. Nothing in CI checks that the scripts and the model agree. Author to confirm whether a check is wanted.
- Database-only features such as the seat-uniqueness filtered index are plain SQL in the scripts (`create_db.sql:717`).

## Alternatives considered

- **EF Core migrations.** Adopted on 2026-07-26 by `fc55b29` (with an `InitialBaseline` and the scripts stamping `__EFMigrationsHistory`), then abandoned for new changes by `7065d9c` on 2026-08-30. The baseline migration and snapshot are still in `Cinema.Data/Migrations/`.
- **Hand-written scripts only, the state before `fc55b29`.** It is the option chosen again; `fc55b29` records its known weakness ("had to be kept in sync by hand").
- Author to confirm: why migrations were dropped after a month, since the repository shows what changed but not the reason.
