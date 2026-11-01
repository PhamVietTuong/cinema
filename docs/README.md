# Documentation Index

> Purpose: Index of this project's knowledge base — business, product, and technical.
> Sources: generated 2026-11-01 from `cinemabe/`, `cinemafe/`, `business/`, `ROADMAP.md`, `CLAUDE.md`, `.github/workflows/ci.yml`, `docker-compose.yml`.
> Last reviewed: 2026-11-01

## Start here

New to this project (human or AI)? Read, in order:
1. [`03-technical/codebase-map.md`](03-technical/codebase-map.md) — where things live.
2. [`01-business/business-rules.md`](01-business/business-rules.md) — what the system enforces and why.
3. [`03-technical/architecture.md`](03-technical/architecture.md) — how the backend is wired.
4. [`00-overview/open-questions.md`](00-overview/open-questions.md) — known gaps/discrepancies, so you don't rediscover them.

Business-rule detail beyond what's summarized here lives in [`../business/`](../business/README.md) (domain model, auth, booking, payments, catalog, admin/staff flows) — already a committed, up-to-date reference; the `01-business/` docs here condense and cite it rather than duplicate it.

## 00-overview

| File | Description |
|---|---|
| [vision.md](00-overview/vision.md) | Product vision — not derivable from code; awaiting human input. |
| [stakeholders.md](00-overview/stakeholders.md) | Who's involved — not derivable from code; awaiting human input. |
| [glossary.md](00-overview/glossary.md) | Domain + technical terms → meaning → code path. |
| [open-questions.md](00-overview/open-questions.md) | All unresolved questions/discrepancies found while writing these docs. |

## 01-business

| File | Description |
|---|---|
| [requirements.md](01-business/requirements.md) | What each user type can currently do, by capability. |
| [user-stories.md](01-business/user-stories.md) | The same, as user stories. |
| [business-rules.md](01-business/business-rules.md) | Every business rule — validations, pricing, state machines, limits — with enforcement citations. |
| [roles-permissions.md](01-business/roles-permissions.md) | The 3-role model (Admin/Customer/TheaterStaff) and what each can do. |
| [market-research.md](01-business/market-research.md) | Not derivable from code; awaiting human input. |
| [financial-plan.md](01-business/financial-plan.md) | Not derivable from code; awaiting human input. |

## 02-product

| File | Description |
|---|---|
| [features.md](02-product/features.md) | Every implemented feature per app, with status vs. ROADMAP.md. |
| [roadmap.md](02-product/roadmap.md) | Restructured feature backlog (from root `ROADMAP.md`). |
| [ui-ux-notes.md](02-product/ui-ux-notes.md) | Screens, routes, key flows, shared CinemaLib components. |

## 03-technical

| File | Description |
|---|---|
| [architecture.md](03-technical/architecture.md) | Backend layering, request flow, SignalR hubs, background services. |
| [codebase-map.md](03-technical/codebase-map.md) | Folder/module → responsibility, "where to change X." |
| [api-spec.md](03-technical/api-spec.md) | Every endpoint on the 3 controllers: route, auth, request/response, errors. |
| [database-schema.md](03-technical/database-schema.md) | Every EF Core entity: fields, constraints, relationships, indexes. |
| [integrations.md](03-technical/integrations.md) | External services (Google/Facebook OAuth, VNPay/MoMo/Stripe, OMDb, notifications). |
| [configuration.md](03-technical/configuration.md) | Every env var / config key, purpose, required/optional. |
| [tech-stack.md](03-technical/tech-stack.md) | Languages, frameworks, key libraries with versions. |

## 04-decisions

| File | Description |
|---|---|
| [template.md](04-decisions/template.md) | ADR template for future technical decisions. |

## 05-process

| File | Description |
|---|---|
| [workflow.md](05-process/workflow.md) | Team process — not derivable from code; awaiting human input. |
| [coding-conventions.md](05-process/coding-conventions.md) | Enforced + observed conventions (cites `CLAUDE.md`). |
| [testing.md](05-process/testing.md) | Test frameworks, how to run tests, what's covered. |
| [deployment.md](05-process/deployment.md) | CI pipeline (`.github/workflows/ci.yml`), Docker Compose; no release/deploy pipeline exists today. |

## 06-meeting-notes

| File | Description |
|---|---|
| [README.md](06-meeting-notes/README.md) | Naming convention for future meeting notes (`YYYY-MM-DD-meeting-topic.md`). None recorded yet. |

---

`docs/` is **not** committed to git (see `.gitignore`). Run `/update-docs` after any session that changes code or business logic to keep it current.
