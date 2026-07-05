# Cinema — Playwright E2E

Browser E2E tests for **CinemaUser** (`:4202`) and **CinemaAdmin** (`:4201`).
Produces a machine-generated **HTML report** (`npm run report`).

## Install

```bash
cd QA-tests/playwright
npm install
npx playwright install chromium
```

## Prerequisites to actually run

1. Backend up: `dotnet run --project Cinema/1-Service/Cinema.Service.WebApiHost` (http://localhost:5102)
2. Seed accounts: `dotnet run --project Cinema/2-Business/Cinema.Business.Tests`
   → `admin@cinema.vn / Admin@123` and `user@cinema.vn / User@123`
3. Frontends served: `ng serve CinemaUser` (4202) and/or `ng serve CinemaAdmin` (4201)

## Run

```bash
npm test                # all specs
npm run test:smoke      # login + navigation (user app)
npm run test:booking    # booking golden path (needs seeded showtime — see below)
npm run test:admin      # admin login + guarded routes
npm run test:headed     # with a visible browser
npm run report          # open the HTML report after a run
```

The booking specs need a real seeded showtime + room:

```bash
CINEMA_SHOWTIME_ID=<guid> CINEMA_ROOM_ID=<guid> npm run test:booking
```

Without those env vars the seat-grid steps `skip` (login + guard assertions still run).

## Override base URLs

```bash
CINEMA_USER_URL=http://localhost:4202 CINEMA_ADMIN_URL=http://localhost:4201 npm test
```

## Layout

```
playwright/
├── playwright.config.ts     # two projects: cinema-user-chromium / cinema-admin-chromium
├── fixtures/personas.ts     # seeded admin + user accounts
├── utils/login.helper.ts    # loginAs / logout (formControlName=emailOrPhone, key cinema_token)
└── specs/
    ├── smoke/01-login-navigation.spec.ts   # CinemaUser
    ├── booking/golden-path.spec.ts         # seat pick → confirmation
    └── admin/01-admin-login.spec.ts        # CinemaAdmin (adminGuard)
```

The HTML report lands in `playwright-report/index.html`.
