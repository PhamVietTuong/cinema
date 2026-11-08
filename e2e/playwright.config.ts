import { defineConfig, devices } from '@playwright/test';

const USER_URL = process.env.CINEMA_USER_URL || 'http://localhost:4202';
const ADMIN_URL = process.env.CINEMA_ADMIN_URL || 'http://localhost:4201';
const STAFF_URL = process.env.CINEMA_STAFF_URL || 'http://localhost:4203';
const API_URL = process.env.CINEMA_API_URL || 'http://localhost:5102';

export default defineConfig({
  testDir: './specs',
  // Specs share one database (seat locks, invoices, reference data) — run serially.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  timeout: 45_000,
  expect: { timeout: 8_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    locale: 'vi-VN',
  },
  projects: [
    { name: 'api', testDir: './specs/api', use: { baseURL: API_URL } },
    { name: 'user', testDir: './specs/user', use: { ...devices['Desktop Chrome'], baseURL: USER_URL } },
    { name: 'admin', testDir: './specs/admin', use: { ...devices['Desktop Chrome'], baseURL: ADMIN_URL } },
    { name: 'staff', testDir: './specs/staff', use: { ...devices['Desktop Chrome'], baseURL: STAFF_URL } },
  ],
});
