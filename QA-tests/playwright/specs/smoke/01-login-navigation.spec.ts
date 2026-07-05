import { test, expect } from '@playwright/test';
import { PERSONAS } from '../../fixtures/personas';
import { loginAs, logout } from '../../utils/login.helper';

/**
 * Smoke — login + base navigation on CinemaUser (:4202).
 * Runs under the `cinema-user-chromium` project (admin specs live in specs/admin/).
 */
test.describe('Smoke — CinemaUser login + navigation', () => {

  test('S01.1 — User logs in and lands on an authenticated route', async ({ page }) => {
    await loginAs(page, PERSONAS.user);
    // Lands on home; a header/nav should be present.
    await expect(page.locator('header, nav, app-header')).toBeVisible();
  });

  test('S01.2 — Public movie list loads', async ({ page }) => {
    await page.goto('/movies');
    await expect(page).toHaveURL(/\/movies/);
    // The page renders without throwing console errors (favicon noise ignored).
    const errors: string[] = [];
    page.on('console', (msg) => { if (msg.type() === 'error') errors.push(msg.text()); });
    await page.waitForLoadState('networkidle');
    expect(errors.filter((e) => !e.includes('favicon'))).toHaveLength(0);
  });

  test('S01.3 — Protected booking route requires auth', async ({ page }) => {
    // Not logged in → authGuard should bounce to login.
    await page.goto('/booking/seats');
    await expect(page).toHaveURL(/\/(auth\/)?login/);
  });

  test('S01.4 — Logout clears the JWT', async ({ page }) => {
    await loginAs(page, PERSONAS.user);
    await logout(page);
    const token = await page.evaluate(() => localStorage.getItem('cinema_token'));
    expect(token).toBeFalsy();
  });
});
