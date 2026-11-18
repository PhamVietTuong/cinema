import { defineConfig, devices } from '@playwright/test';

// Real stack, no mocks. Prereqs (see e2e/README.md): SQL Server DB, cinema API on :5102,
// CinemaUser :4202, CinemaAdmin :4201, CinemaStaff :4203, all on the same dev DB.
//
// One chromium project (plus the compat projects when E2E_COMPAT=1); each spec file picks its app with `test.use({ baseURL })` (USER_URL /
// ADMIN_URL / STAFF_URL / API_URL from tests/helpers.ts). Override the targets via CINEMA_*_URL.
// Compatibility (cross-browser / cross-platform): the default run is chromium only so CI stays fast. Set
// E2E_COMPAT=1 (npm run test:compat) to also run on Firefox, WebKit (Safari engine) and mobile emulations.
const compat = process.env.E2E_COMPAT === '1';
const projects = [
  { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ...(compat
    ? [
        { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
        { name: 'webkit', use: { ...devices['Desktop Safari'] } },
        { name: 'mobile-chrome', use: { ...devices['Pixel 7'] } },
        { name: 'mobile-safari', use: { ...devices['iPhone 14'] } },
      ]
    : []),
];

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  fullyParallel: false, // shared dev DB — parallel runs would race on the same rows.
  workers: 1,
  retries: 0,
  reporter: [['list']],
  globalSetup: require.resolve('./global-setup.ts'),
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects,
});
