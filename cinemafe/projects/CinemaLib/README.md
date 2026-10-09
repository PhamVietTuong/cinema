# CinemaLib

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 21.2.0.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the library, run:

```bash
ng build cinema-lib
```

This command will compile your project, and the build artifacts will be placed in the `dist/` directory.

### Publishing the Library

Once the project is built, you can publish your library by following these steps:

1. Navigate to the `dist` directory:

   ```bash
   cd dist/cinema-lib
   ```

2. Run the `npm publish` command to publish your library to the npm registry:
   ```bash
   npm publish
   ```

## Running unit tests

To execute unit tests with the [Karma](https://karma-runner.github.io) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.

## Shared UI inventory

Any UI used by two or more apps lives here as a configurable `cl-*` component. Check this list before writing markup in an app.

| Component | Purpose | Inputs |
| --- | --- | --- |
| `cl-app-shell` | Sidenav, topbar, user card, language/theme switch, logout | `brand`, `menu` (`NavSection[]`), `user`, ... |
| `cl-login-form` | Split-screen sign-in | `brand`, `titleKey`, `allowedRoles`, `noticeKey`, `noticeLinkKey`, `noticeLinkUrl`; output `rejected` |
| `cl-status-pill` | Coloured status pill; labels/colours from `cinema.model.ts` (`statusPillSpec`) | `kind` (`invoice` / `storagePlan` / `stockLevel`), `value` |
| `cl-empty-state` | "Nothing here" placeholder | `messageKey`, `hintKey`, `icon` |
| `cl-filter-bar` | Filter card for paged lists, bound to the page's `searchForm` | `form`, `fields` (`FilterBarField[]`: text / select / toggle), `titleKey`; output `filtersChange` |
| `cl-reason-dialog` | Confirm / reject / receive with reason code, note and quantity lines; open via `DialogService.openReasonDialog()` | `ReasonDialogData`: `titleKey`, `confirmKey`, `confirmColor`, `hintKey`, `codes`, `note`, `lines` |
| `cl-confirm-dialog` | Yes/No confirmation (`DialogService.openConfirmDialog()`) | `ConfirmDialogData` |

Enum label maps and pill colours always live in `interfaces/cinema.model.ts`, never inline in a feature component.
