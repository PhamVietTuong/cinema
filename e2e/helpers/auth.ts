import { Page, expect } from '@playwright/test';
import { Persona } from '../fixtures/personas';

const TOKEN_KEY = 'cinema_token';

/** Drives the real login form (shared contract of all three apps). */
export async function loginAs(page: Page, persona: Persona): Promise<void> {
  await page.goto('/auth/login');
  await page.locator('[formcontrolname="emailOrPhone"]').fill(persona.email);
  await page.locator('[formcontrolname="password"]').fill(persona.password);
  await page.locator('button[type="submit"]').first().click();
  await page.waitForURL((url) => !url.pathname.includes('/auth/login'), { timeout: 20_000 });
  expect(await getToken(page), 'JWT should be stored after login').toBeTruthy();
}

export async function getToken(page: Page): Promise<string | null> {
  return page.evaluate((key) => localStorage.getItem(key), TOKEN_KEY);
}

/** Drops the session without touching the UI, so guard specs start anonymous. */
export async function clearSession(page: Page): Promise<void> {
  await page.goto('/auth/login');
  await page.evaluate(() => {
    localStorage.clear();
    sessionStorage.clear();
  });
}
