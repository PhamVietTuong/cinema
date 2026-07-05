import { defineConfig, devices } from '@playwright/test';

// CinemaUser (public site) on 4202, CinemaAdmin (back-office) on 4201 (per angular.json serve ports).
// Override via env vars when serving on other ports.
const BASE_URL_USER = process.env.CINEMA_USER_URL || 'http://localhost:4202';
const BASE_URL_ADMIN = process.env.CINEMA_ADMIN_URL || 'http://localhost:4201';

export default defineConfig({
  testDir: './specs',
  // Booking flow mutates shared DB state (seat locks, invoices) → run serially.
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1, // 1 worker = no DB race between specs
  reporter: [
    ['html', { open: 'never' }],            // → playwright-report/index.html (npm run report)
    ['list'],
    ['json', { outputFile: 'test-results/results.json' }],
  ],
  timeout: 30_000,
  expect: { timeout: 5_000 },
  use: {
    baseURL: BASE_URL_USER,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      // Public-facing CinemaUser app — smoke + booking specs.
      name: 'cinema-user-chromium',
      use: { ...devices['Desktop Chrome'], baseURL: BASE_URL_USER },
      testIgnore: /.*\/admin\/.*\.spec\.ts/,
    },
    {
      // Back-office CinemaAdmin app — only specs under specs/admin/.
      name: 'cinema-admin-chromium',
      use: { ...devices['Desktop Chrome'], baseURL: BASE_URL_ADMIN },
      testMatch: /.*\/admin\/.*\.spec\.ts/,
    },
  ],
});
