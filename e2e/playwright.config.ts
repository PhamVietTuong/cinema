import { defineConfig, devices } from '@playwright/test';

// Real stack, no mocks. Prereqs (see e2e/README.md): SQL Server DB, cinema API on :5102,
// CinemaUser :4202, CinemaAdmin :4201, CinemaStaff :4203, all on the same dev DB.
//
// One chromium project; each spec file picks its app with `test.use({ baseURL })` (USER_URL /
// ADMIN_URL / STAFF_URL / API_URL from tests/helpers.ts). Override the targets via CINEMA_*_URL.
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
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
