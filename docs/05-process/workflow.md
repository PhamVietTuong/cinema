# Workflow

> Purpose: Team process and ways of working (sprints, reviews, ceremonies).
> Sources: `AGENTS.md` (Git section), `.github/workflows/ci.yml`. Sprint cadence and review ceremonies are not derivable from the repository.
> Last reviewed: 2026-11-12

## Git workflow (from `AGENTS.md`)

- Run `git fetch` + `git pull` before creating a branch; work on `feature/<name>`; merge to `master` when the feature is done; ask whether to open a PR after finishing a feature.
- Commit at each compiling milestone, not one giant commit per task.
- Generated screenshots go to `E:\cinema\tasks`, run/serve/QA logs to `E:\cinema\logs` (both git-ignored).

Owner input needed: sprint cadence, review ceremonies and who approves merges (nothing in the repository records them).

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
