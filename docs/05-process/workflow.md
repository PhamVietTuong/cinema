# Workflow

> Purpose: Team process and ways of working (sprints, reviews, ceremonies).
> Sources: (none — not derivable from code; needs human input)
> Last reviewed: 2026-11-12

## Quality gates a PR must pass

CI (`.github/workflows/ci.yml`) runs backend build + tests + analyzer ratchet, frontend lint ratchet + tests + builds,
Docker image build, e2e, and (informational) gitleaks and dependency audits. See `deployment.md` for baselines.
Ratchets only go down: when a PR reduces the ESLint or analyzer warning count, lower the baseline in the same PR
(`cinemafe/package.json` `lint:ci`, `.github/scripts/analyzer-baseline.txt`).

## Recommended branch protection for `master` (a GitHub setting, not applied by the repo)

- Require a pull request before merging, with **1 approving review**.
- Require status checks to pass: `Backend (build + test)`, `Frontend (lint + test + build)`, `E2E (Playwright)`,
  `Docker image (build only, no push)`. Add `Secret scan (gitleaks)` once it is blocking.
- Require branches to be up to date before merging; disallow force pushes and deletion.

## Not covered

Release/deploy, monitoring and alerting do not exist yet (see `deployment.md`).

## Related
- See [[../../CLAUDE.md]] for the AI coding agent's own default workflow (explorer → planner → code → tests → tester → reviewer) and this project's git feature-branch convention.
