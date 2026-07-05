# Cinema — Flow regression tests

> Business cross-layer regression test specs for the Cinema app.
> Ported from the SIH QA harness. v1.0 · 2026-06-20

---

## What this is

This folder holds **business regression test specifications** (YAML), one file per flow: `<flow-id>.yaml`.

After modifying code that touches a flow, run `/test-flow <flow-id>`. A background agent then:

1. **Static analysis** — re-reads the files in scope and checks code patterns (`must_contain`, `forbidden_patterns`, `must_use_enum`).
2. **Build + unit tests** — `dotnet build` / `dotnet test` (the real xUnit suite in `Cinema.Business.Tests`) + `ng build`.
3. **E2E playbook** — emits a manual step-by-step script with assertions (not auto-run).
4. **Reports** — always writes the rolling log `QA-tests/FLOW-TEST-RESULTS.md` and a styled `QA-tests/reports/<flow>-<date>/REPORT.html`; flags a P0 alert section if a regression indicator trips.
5. **Bug tickets** — for a **real** defect (not just a stale check), writes a developer-ready `QA-tests/auto-bugs/BUG-<NNNN>-<slug>.md` (repro + expected/actual + suggested fix + checklist) and, when obvious, a matching `QA-tests/patch-suggestions/BUG-<NNNN>-patch.<ext>`.

The test does **not block** your work — it runs in parallel.

### Outputs at a glance

| Path | When | Contents |
|---|---|---|
| `QA-tests/FLOW-TEST-RESULTS.md` | every run | rolling log, newest on top |
| `QA-tests/reports/<flow>-<date>/REPORT.html` | every run | styled, non-technical summary |
| `QA-tests/auto-bugs/BUG-<NNNN>-<slug>.md` | real defect found | per-bug ticket for the dev |
| `QA-tests/patch-suggestions/BUG-<NNNN>-patch.<ext>` | real defect + obvious fix | suggested patch (not auto-applied) |

## Flows available (v1.0)

| ID | Description | Layers | Status |
|---|---|---|---|
| `booking-seat-lock` | Seat locking (`_lockedSeats` ConcurrentDictionary, 5-min expiry) + booking → Invoice(Pending) → ConfirmPayment(Paid) → Cancel guards | Business + Data + Service + FE booking | ✅ v1.0 |
| `auth-login` | Login/Register, JWT issuance, role-based `[Authorize]` gating | Business + Service + FE auth | ✅ v1.0 |
| `movie-admin` | Admin movie/catalog CRUD, Admin-role gating, soft-delete (IsActive=false) | Business + Service + FE admin | ✅ v1.0 |

## How to invoke

From the cinema repo with Claude Code:

```
/test-flow booking-seat-lock
```

Omit the argument to list available flows.

## YAML schema

```yaml
flow_id: <kebab-case-id>
name: "..."
description: "..."
layers: [list]
version: 1.0

trigger_paths:            # files whose change should trigger this flow
  - "Cinema/2-Business/.../*.cs"
trigger_keywords:         # identifiers that, if seen in a diff, implicate this flow
  - "MethodName"

static_checks:            # §2 — regex/read checks on the code
  - id: SC-XXX-01
    severity: P0|P1|P2
    file: "path"          # or files: [...]
    method: "MethodName"  # optional — isolate one method
    must_contain: [{pattern, rationale}]
    must_not_contain: [{pattern, rationale}]
    must_use_enum: [{enum_name, values_expected, rationale}]
    forbidden_patterns: [{regex, rationale}]

build_checks:             # §3 — dotnet build / dotnet test / ng build
  - id: BC-XXX-01
    cwd: "E:/cinema/cinemabe"
    command: "dotnet test ..."
    expected_exit_code: 0
    timeout_seconds: 300
    skip_if_unchanged: ["glob/**"]

playbook:                 # §4 — manual E2E
  prerequisites: [...]
  scenarios: [{id, title, severity, steps: [{step, action, expected}]}]

regression_indicators:    # §5 — what a failure means in business terms
  - id: RI-XXX-01
    severity: P0|P1|P2
    detection: "static SC-XXX-01 OR playbook PB-XXX-01 step 2"

reporting:                # §6
  output_path: "QA-tests/FLOW-TEST-RESULTS.md"
  alert_on_p0: true
```

## Assumed limits (v1.0)

- **Static analysis** = regex/grep + method read. No C# AST parsing.
- **Build check** runs `dotnet build`, and where wired, the real `dotnet test` xUnit suite.
- **Playbook is manual** — the agent produces the script; a human runs the browser steps (no isolated seeded env auto-spun).

## Reference

- Flow registry (keyword→flow): `.claude/hooks/business-flows.json`
- Slash command: `.claude/commands/test-flow.md`
- Playwright E2E (the other test system): `QA-tests/playwright/`
