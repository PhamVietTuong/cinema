---
description: Run a business regression test on a cinema flow (static checks + build/test + manual playbook). Spawns a background agent that reports into QA-tests/FLOW-TEST-RESULTS.md.
argument-hint: <flow-id> (e.g. booking-seat-lock, auth-login, movie-admin)
---

# /test-flow — Cinema business regression test

You will run a regression test on a cinema business flow. The flow_id is the argument: `$ARGUMENTS`.

## Step 1 — Load the flow spec

Read `QA-tests/flow-tests/${ARGUMENTS}.yaml`. If it does not exist, list the available flows (`QA-tests/flow-tests/*.yaml`) and tell the user which names are valid. If no argument was given, list the flows and ask which to run.

## Step 2 — Identify the changed scope

Get recent changes and cross them against the YAML `trigger_paths`:

```bash
git -C E:/cinema diff --name-only
git -C E:/cinema diff --name-only HEAD~5..HEAD
```

If NO changed file matches the flow scope, tell the user ("no changes detected on the `${ARGUMENTS}` scope — run anyway?") and let them choose.

## Step 3 — Spawn a background agent

Launch an Agent in **background** (`run_in_background: true`) with this prompt:

```
You are the cinema business-regression test agent. Task: execute flow test `${ARGUMENTS}`.

Context:
- Spec YAML: E:/cinema/QA-tests/flow-tests/${ARGUMENTS}.yaml
- Backend repo: E:/cinema/cinemabe
- Frontend repo: E:/cinema/cinemafe

Execute in order:

1. Static checks (§2):
   For each static_check:
   - Read the file(s) in `file`/`files`. If `method` is set, isolate that method.
   - For each must_contain.pattern → verify present (FAIL if missing).
   - For each must_not_contain.pattern → verify absent (FAIL if present).
   - For each must_use_enum → verify the enum members exist and are referenced by name (not hardcoded ints).
   - For each forbidden_patterns.regex → verify NO match (match = FAIL).
   - Mark each PASS / FAIL / SKIP (skip if file unchanged and skip_if_unchanged applies).

2. Build checks (§3):
   For each build_check: cd to `cwd`, run `command` with `timeout_seconds`, compare exit code to `expected_exit_code`.
   Honor `skip_if_unchanged` globs. Capture build/test error output when it fails.

3. Playbook (§4): do NOT auto-run. Emit a Markdown block listing prerequisites + each scenario's steps & expected, ready for manual execution.

4. Regression indicators (§5): for each, cross-reference the static/build results.
   - detection cites a static check that FAILed → indicator TRIGGERED.
   - detection cites a playbook step → indicator "verify manually".

5. Reporting: write the report to E:/cinema/QA-tests/FLOW-TEST-RESULTS.md using this format:

   # Flow Test Result — ${ARGUMENTS} — YYYY-MM-DD HH:MM
   **Flow**: <name>   **Layers**: <layers>
   **Changed scope detected**: <files>

   ## §1 Static checks (X/Y PASS)
   | Check | Severity | Status | Detail |
   |---|---|---|---|

   ## §2 Build + test checks (X/Y PASS)
   | Check | Status | Output (excerpt if fail) |
   |---|---|---|

   ## §3 Playbook to run manually
   <the scenarios>

   ## §4 Regression indicators
   | ID | Severity | Status | Source |
   |---|---|---|---|

   ## §5 Summary
   - X of Y regression indicators triggered
   - Build/tests: OK / KO
   - Action expected: ...

6. Per-bug export (only for REAL defects):
   A triggered P0/P1 indicator is a REAL defect when it reflects an actual code problem
   — NOT when it is merely a check-contract mismatch (the spec pattern is stale but the
   code is correct). Judge this explicitly.
   - If the trigger is a stale check → do NOT write a bug file. Note in §5 that the YAML
     check should be reconciled, and stop.
   - If it is a real defect → for each one, write `E:/cinema/QA-tests/auto-bugs/BUG-<NNNN>-<slug>.md`
     using the format in `QA-tests/auto-bugs/BUG-TEMPLATE.md` (next free 4-digit number;
     fill frontmatter + Description + Steps to reproduce + Expected vs Actual + Suggested fix
     + Verification checklist). If an obvious fix exists, also write
     `E:/cinema/QA-tests/patch-suggestions/BUG-<NNNN>-patch.<cs|ts|sql>` — a focused snippet,
     never auto-applied.

7. HTML report (always): write a self-contained, styled `E:/cinema/QA-tests/reports/<flow-id>-<YYYY-MM-DD>/REPORT.html`
   summarizing this run for non-technical reading — verdict banner (🟢 all green / 🔴 N P0),
   the static + build tables, the triggered indicators, and links (relative) to any
   BUG-<NNNN>.md emitted. Inline all CSS; no external assets.

   ## §5 Summary
   - X regression indicators triggered (of Y)
   - Build/tests: OK / KO
   - Action expected: ...

6. If any P0 regression indicator is triggered by static or build, prepend a
   "🔴 P0 ALERT" section at the top of the report naming the triggered indicators
   and the action expected before push.

Report back to the orchestrator: static PASS/FAIL/SKIP counts, build PASS/FAIL/SKIP counts,
indicators triggered, and the report path.
```

## Step 4 — Confirm

Tell the user the test is running in background, where the spec and report live, and that you'll surface the summary when the agent finishes. If it finishes during the session, show the §5 Summary and, if red, propose the fix loop (re-read the invariant, fix, re-run `/test-flow ${ARGUMENTS}`).

## Notes

- The background agent runs in its own context — give it absolute paths and the full format in the prompt.
- If the YAML is malformed or a referenced file is missing, fail gracefully and write the diagnosis into the report.
- Goal is NON-blocking: the dev keeps working; the result arrives in background.
