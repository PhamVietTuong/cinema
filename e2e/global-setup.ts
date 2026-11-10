import fs from 'node:fs';
import path from 'node:path';
import { chromium, type FullConfig } from '@playwright/test';
import { ADMIN, ADMIN_URL, API_URL, CUSTOMER, SESSION, STAFF_URL, USER_URL } from './tests/helpers';

function causeChainMessage(error: unknown): string {
  const parts: string[] = [];
  let current: unknown = error;
  while (current instanceof Error) {
    parts.push(current.message);
    current = (current as { cause?: unknown }).cause;
  }
  return parts.length ? parts.join(' <- ') : String(error);
}

// Fails fast with a clear message instead of letting every test time out obscurely when
// the manual dev stack isn't up — see e2e/README.md for how to start it.
async function checkReachable(url: string, label: string): Promise<void> {
  try {
    const response = await fetch(url);
    if (!response.ok && response.status !== 404) {
      throw new Error(`${label} at ${url} responded ${response.status}`);
    }
  } catch (error) {
    throw new Error(
      `${label} is not reachable at ${url}. Start it first (see e2e/README.md). Cause: ${causeChainMessage(error)}`
    );
  }
}

// Logs in through the real form once per app and saves the session for the specs that only need
// "a signed-in user" (the login endpoint is rate limited, see SESSION in tests/helpers.ts).
async function saveSession(baseURL: string, credentials: { email: string; password: string }, file: string) {
  const browser = await chromium.launch();
  try {
    const page = await (await browser.newContext({ baseURL })).newPage();
    await page.goto('/auth/login');
    await page.locator('[formcontrolname="emailOrPhone"]').fill(credentials.email);
    await page.locator('[formcontrolname="password"]').fill(credentials.password);
    await page.locator('button[type="submit"]').first().click();
    await page.waitForURL((url) => !url.pathname.includes('/auth/login'), { timeout: 30_000 });
    // storageState only captures localStorage; the token is in sessionStorage unless "remember me".
    await page.evaluate(() => {
      for (let i = 0; i < sessionStorage.length; i++) {
        const key = sessionStorage.key(i)!;
        localStorage.setItem(key, sessionStorage.getItem(key)!);
      }
    });
    fs.mkdirSync(path.dirname(file), { recursive: true });
    await page.context().storageState({ path: file });
  } catch (error) {
    throw new Error(`Could not sign ${credentials.email} in at ${baseURL}: ${causeChainMessage(error)}`);
  } finally {
    await browser.close();
  }
}

export default async function globalSetup(_config: FullConfig): Promise<void> {
  await Promise.all([
    checkReachable(`${API_URL}/health`, 'cinema API'),
    checkReachable(USER_URL, 'CinemaUser dev server'),
    checkReachable(ADMIN_URL, 'CinemaAdmin dev server'),
    checkReachable(STAFF_URL, 'CinemaStaff dev server'),
  ]);

  await saveSession(USER_URL, CUSTOMER, SESSION.user);
  await saveSession(ADMIN_URL, ADMIN, SESSION.admin);
  await saveSession(STAFF_URL, ADMIN, SESSION.staff);
}
