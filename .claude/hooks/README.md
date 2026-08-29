# Cinema — anti-regression PreToolUse hook

A `PreToolUse` hook that runs before every `Edit` / `Write` / `MultiEdit` /
`NotebookEdit` and confronts the change with the project's business invariants.

## What it does

1. Detects which cinema module the touched file belongs to (`module-paths-mapping.json` → `modules`).
2. Matches the diff text against `diff_rules` regexes (P0 / P1 / P2).
3. Matches the diff text against `business-flows.json` `trigger_keywords` (cross-module flows).
4. Reacts by severity:
   - **P0 match** (e.g. a status hardcoded as a magic int) → `permissionDecision: "ask"` — Claude must confirm before writing.
   - **Module/flow matched, no P0** → soft `additionalContext` warning injected into the session (non-blocking).
   - **Nothing matched** → silent allow (zero friction).

Pure Node, no external deps (no `jq`), cross-platform, ~tens of ms.

## Files

| File | Role |
|---|---|
| `check-business-invariants.js` | the hook (reads the tool payload on stdin) |
| `module-paths-mapping.json` | module path/entity map + `diff_rules` |
| `business-flows.json` | cross-module flow keyword registry (shared with `/test-flow`) |

## Wiring

Already wired in `.claude/settings.json`:

```json
{ "hooks": { "PreToolUse": [ { "matcher": "Edit|Write|MultiEdit|NotebookEdit",
  "hooks": [ { "type": "command",
    "command": "node \"${CLAUDE_PROJECT_DIR}/.claude/hooks/check-business-invariants.js\"",
    "timeout": 5 } ] } ] } }
```

Run `/hooks` inside Claude Code to confirm it's loaded.

## Test it manually

```bash
printf '%s' '{"tool_name":"Edit","tool_input":{"file_path":"cinemabe/.../BookingManager.cs","old_string":"Status = InvoiceStatus.Pending","new_string":"Status = (InvoiceStatus)1"}}' \
  | node .claude/hooks/check-business-invariants.js
```
→ emits a `permissionDecision: "ask"` (P0: status hardcoded as an int).

## Maintenance

- **New invariant / module** → add to `module-paths-mapping.json` (`modules` and/or `diff_rules`).
- **Too many false positives on a rule** → tighten or remove its `pattern`.
- **Disable for a session** → comment the `hooks` block in `.claude/settings.json` and restart.

Rules currently enforced: `DIFF-STATUS-01` (P0, magic-int status), `DIFF-DELETE-01`
(P1, physical delete on a soft-delete entity), `DIFF-AUTH-01` (P1, `[AllowAnonymous]` added).
