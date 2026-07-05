# auto-bugs/ — per-defect tickets

When a flow test (`/test-flow <flow-id>`) finds a **real** defect (a triggered P0/P1
regression indicator that reflects an actual code problem, not a check-contract
mismatch), it writes one Markdown file here:

```
BUG-<NNNN>-<short-slug>.md
```

Each file is a self-contained, developer-ready ticket: frontmatter (id, priority,
affected files, suggested owner) + Description + Steps to reproduce + Expected vs
Actual + Suggested fix + Verification checklist. A matching patch (when one is
obvious) goes in `../patch-suggestions/BUG-<NNNN>-patch.<ext>`.

The rolling run log (`../FLOW-TEST-RESULTS.md`) still records every run; these
files are the deep-dive for the specific bugs worth fixing.

Numbering: 4-digit, monotonically increasing. Check the highest existing
`BUG-NNNN-*.md` and use the next number.

Status values in frontmatter: `to-verify` → `in-progress` → `fixed` → `verified`.

See `BUG-TEMPLATE.md` for the exact format.
