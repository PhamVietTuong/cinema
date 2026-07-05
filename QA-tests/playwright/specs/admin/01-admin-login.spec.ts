import { test, expect } from '@playwright/test';
import { PERSONAS } from '../../fixtures/personas';
import { loginAs } from '../../utils/login.helper';

/**
 * Smoke — CinemaAdmin back-office (:4201).
 * Runs under the `cinema-admin-chromium` project (testMatch on specs/admin/).
 */
test.describe('Smoke — CinemaAdmin login + guarded routes', () => {

  test('A01.1 — Admin logs in and reaches the dashboard', async ({ page }) => {
    await loginAs(page, PERSONAS.admin);
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('A01.2 — Standard user is blocked from admin area (adminGuard)', async ({ page }) => {
    await loginAs(page, PERSONAS.user);
    await page.goto('/movies'); // admin movies-management, behind adminGuard
    // adminGuard should deny: either bounced to login or kept off the admin route.
    await expect(page).not.toHaveURL(/\/movies$/);
  });

  test('A01.3 — Movies management list renders for admin', async ({ page }) => {
    await loginAs(page, PERSONAS.admin);
    await page.goto('/movies');
    await expect(page).toHaveURL(/\/movies/);
    await expect(page.locator('table, .grid, mat-table')).toBeVisible();
  });
});
