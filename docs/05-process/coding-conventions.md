# Coding Conventions

> Purpose: Enforced and observed coding conventions for the backend and frontend.
> Sources: E:\cinema\CLAUDE.md, E:\cinema\cinemabe\.editorconfig, E:\cinema\cinemafe\eslint.config.js
> Last reviewed: 2026-11-04

Most conventions in this repo are documented in `CLAUDE.md` at the repo root. This page cites
those directly and adds what's enforced by config files that `CLAUDE.md` doesn't spell out.

## Already documented in CLAUDE.md (cited, not re-derived)

| Rule | Where in CLAUDE.md |
|---|---|
| Always use block statements `{ }`, never one-liner control-flow bodies | "Working conventions to preserve" |
| Never add a 4th controller — stay at `CinemaController`, `IdentityController`, `PaymentController` | "Working conventions to preserve" |
| No partial controller files — one `CinemaController.cs` with `#region` blocks | "Working conventions to preserve" |
| Layer dependency direction — higher-numbered layers (`1-Service` > `2-Business` > `3-Data` > `4-Foundation`) may only depend one tier deeper | "Architecture — 4 numbered layers" |
| Controllers use try/catch + `LogProvider.Current` pattern (`{GetType().Name}.{Method} being awakened…` on entry, `Fatal` on exception, `StatusCode(500, e.Message)`) | "Working conventions to preserve" |
| All list/search endpoints take `PagingSearchDTO` in the body via POST, return `DefaultSearchResults<T>`; filters via `search.Filters.GetGuid("key")` etc. | "Working conventions to preserve" |
| Query efficiency: `AsNoTracking()` on reads, avoid N+1, project columns, filter/count/page at the DB | "Query Efficiency" section (5.1/5.2) |
| Don't hand-edit NSwag-generated files (`Cinema.Service.Clients/**`, `*-http.service.ts`) | "Working conventions to preserve" |
| Datetime mapping — new entities pick up SQL `datetime` (not `datetime2`) automatically | "Working conventions to preserve" / "EF Core quirk" |
| Schema changes go through hand-edited `create_db.sql`/`upgrade_db.sql`, never `dotnet ef migrations add` (user memory rule, consistent with current repo practice) | — (memory rule; also noted in root `CLAUDE.md`) |
| Use `nameof(X)` instead of a hand-typed string literal when the string is meant to match a type/class/method name (user memory rule) | — |
| NSwag enum→label lookups belong in `CinemaLib`'s `cinema.model.ts`, never inline in a feature component (user memory rule) | — |
| Shared UI used by 2+ Angular apps belongs in one configurable `CinemaLib` component, never copy-pasted (user memory rule) | — |
| Single shared `AuditLog` table for all logging needs, never a new table per feature | "Working conventions to preserve" |

## From `.editorconfig` (`cinemabe/.editorconfig`) — not mentioned in CLAUDE.md

```ini
root = true
[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space

[*.cs]
indent_size = 4
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
dotnet_sort_system_directives_first = true
dotnet_style_require_accessibility_modifiers = for_non_interface_members:suggestion

[*.{json,yml,yaml,csproj,props}]
indent_size = 2

[*.sql]
indent_size = 4
```

- Line endings are **CRLF**, UTF-8, with a trailing newline and trailing whitespace trimmed, repo-wide.
- C# files: 4-space indent, **file-scoped namespaces** preferred, `var` preferred when the type is
  apparent from the right-hand side, `System.*` usings sorted first, explicit accessibility
  modifiers required on non-interface members.
- JSON/YAML/csproj/props: 2-space indent. SQL: 4-space indent.

There is no `.editorconfig` under `cinemafe/` — frontend formatting is governed only by ESLint
(below) and Prettier defaults where used by the IDE.

## From `cinemafe/eslint.config.js` — not mentioned in CLAUDE.md

- Flat config (`tseslint.config(...)`), extends `eslint:recommended`, `typescript-eslint:recommended`,
  and `angular-eslint` (`tsRecommended` for `.ts`, `templateRecommended` + `templateAccessibility`
  for `.html`).
- Ignored entirely: `dist/**`, `out-tsc/**`, `coverage/**`, `node_modules/**`, `.angular/**`, `**/*.spec.ts`.
- Also ignored (generated, never hand-edited): `projects/CinemaLib/src/lib/services/*-http.service.ts`
  (NSwag) and `projects/CinemaLib/src/lib/svg/**` (icon barrel from `npm run generate-icons`).
- Component/directive selector prefixes are enforced: `app` or `cl`, element selectors kebab-case,
  attribute selectors camelCase.
- **Severity policy** (explicit comment in the file): linting was introduced against an existing
  codebase with 614 pre-existing violations. Rules that were driven to zero are `error`; rules with
  a backlog that is "a refactor of its own" are kept at `warn` so CI stays meaningful without being
  permanently red:
  - `warn`: `@angular-eslint/template/prefer-control-flow` (365 hits — `*ngIf`/`*ngFor` → `@if`/`@for`),
    `@angular-eslint/template/label-has-associated-control` (138, a11y),
    `@angular-eslint/prefer-inject` (60 — constructor DI → `inject()`),
    `@typescript-eslint/no-explicit-any` (82 — leaned on at the NSwag/store boundary),
    `@angular-eslint/prefer-standalone` (both app shells are `standalone: false` by design),
    `@angular-eslint/template/click-events-have-key-events`,
    `@angular-eslint/template/interactive-supports-focus`,
    `@angular-eslint/template/no-negated-async` (7 hits — rewrite isn't behaviorally equivalent
    everywhere; see the file's comment).
  - `error`: everything else in the extended rule sets, plus `@typescript-eslint/no-unused-vars`
    (with `argsIgnorePattern`/`varsIgnorePattern: '^_'`, `caughtErrors: 'none'`, `ignoreRestSiblings: true`
    to allow `const { secret, ...rest } = obj`).
  - The file's own comment: "Promote a rule to `error` once its backlog is cleared."
- CI (`.github/workflows/ci.yml`) runs `npm run lint` and fails only on `error`-level violations —
  the warn-level backlog above does not fail the build today.

<!-- TODO: confirm with team whether cinemafe should eventually get its own .editorconfig, or whether ESLint + Prettier is considered sufficient. -->
