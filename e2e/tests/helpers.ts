import path from 'node:path';
import { errors, expect, type APIRequestContext, type Page } from '@playwright/test';

export const USER_URL = process.env.CINEMA_USER_URL ?? 'http://localhost:4202';
export const ADMIN_URL = process.env.CINEMA_ADMIN_URL ?? 'http://localhost:4201';
export const STAFF_URL = process.env.CINEMA_STAFF_URL ?? 'http://localhost:4203';
export const API_URL = process.env.CINEMA_API_URL ?? 'http://localhost:5102';

export interface Credentials {
  email: string;
  password: string;
}

// Demo accounts: admin@/staff@ come from the seed (`dotnet run --project
// Cinema/2-Business/Cinema.Business.Tests`), khach01@ from sql/insert_db.sql. Override per env.
export const ADMIN: Credentials = {
  email: process.env.E2E_ADMIN_EMAIL ?? 'admin@cinema.vn',
  password: process.env.E2E_ADMIN_PASSWORD ?? 'Admin@123',
};
export const CUSTOMER: Credentials = {
  email: process.env.E2E_USER_EMAIL ?? 'khach01@cinema.vn',
  password: process.env.E2E_USER_PASSWORD ?? 'Khach01@123',
};
export const STAFF: Credentials = {
  email: process.env.E2E_STAFF_EMAIL ?? 'staff@cinema.vn',
  password: process.env.E2E_STAFF_PASSWORD ?? 'Staff@123',
};

// Sessions saved once by global-setup.ts. The login endpoint is rate limited (10/min per IP), so
// logging in through the form in every test would make the suite flaky. Each app has its own
// origin, hence its own file.
export const SESSION = {
  user: path.resolve(__dirname, '../.auth/user.json'),
  admin: path.resolve(__dirname, '../.auth/admin.json'),
  staff: path.resolve(__dirname, '../.auth/staff.json'),
};
export const ANONYMOUS = { cookies: [], origins: [] };

const TOKEN_KEY = 'cinema_token';

/** Drives the real login form (same contract in all three apps). */
export async function login(page: Page, credentials: Credentials): Promise<void> {
  await page.goto('/auth/login');
  await page.locator('[formcontrolname="emailOrPhone"]').fill(credentials.email);
  await page.locator('[formcontrolname="password"]').fill(credentials.password);
  await page.locator('button[type="submit"]').first().click();
  await page.waitForURL((url) => !url.pathname.includes('/auth/login'), { timeout: 20_000 });
  expect(await getToken(page), 'JWT should be stored after login').toBeTruthy();
}

// The token lives in sessionStorage unless "remember me" is ticked.
export async function getToken(page: Page): Promise<string | null> {
  return page.evaluate((key) => localStorage.getItem(key) ?? sessionStorage.getItem(key), TOKEN_KEY);
}

export async function dropToken(page: Page): Promise<void> {
  await page.goto('/auth/login'); // storage is only reachable once a document of the app is loaded
  await page.evaluate((key) => {
    localStorage.removeItem(key);
    sessionStorage.removeItem(key);
  }, TOKEN_KEY);
}

export async function clearSession(page: Page): Promise<void> {
  await page.goto('/auth/login');
  await page.evaluate(() => {
    localStorage.clear();
    sessionStorage.clear();
  });
}

/** Opens a route and asserts it rendered: stayed on the path, no uncaught errors, non-empty body. */
export async function expectRouteRenders(page: Page, route: string): Promise<void> {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto(route);
  await page.waitForLoadState('networkidle');

  expect(new URL(page.url()).pathname).toBe(route);
  await expect(page.locator('body')).not.toBeEmpty();
  expect(errors, `uncaught errors on ${route}`).toEqual([]);
}

export const PAGING = { pageIndex: 1, pageSize: 5 };

/** Logs in over the API and returns an Authorization header. */
export async function bearer(request: APIRequestContext, credentials: Credentials): Promise<Record<string, string>> {
  const res = await request.post('/api/Identity/Login', {
    data: { emailOrPhone: credentials.email, password: credentials.password },
  });
  if (!res.ok()) {
    throw new Error(`Login failed for ${credentials.email}: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const token = body.token ?? body.accessToken;
  if (!token) {
    throw new Error(`Login response has no token field: ${JSON.stringify(Object.keys(body))}`);
  }
  return { Authorization: `Bearer ${token}` };
}

/** Closes the marketing splash banner if one is active; it opens on first visit and blocks every click behind it. */
export async function dismissSplashBanner(page: Page): Promise<void> {
  const close = page.locator('.sb-close');
  try {
    await close.waitFor({ state: 'visible', timeout: 3_000 });
  } catch (error) {
    if (error instanceof errors.TimeoutError) {
      return; // no banner is active
    }
    throw error;
  }
  await close.click();
  await close.waitFor({ state: 'hidden' });
}
