import { test, expect } from '@playwright/test';
import { ADMIN, ADMIN_URL, ANONYMOUS, CUSTOMER, clearSession, login } from './helpers';

test.use({ baseURL: ADMIN_URL });

test.describe('CinemaAdmin: authentication and guards', () => {
  test.use({ storageState: ANONYMOUS });
  test('anonymous visitors are sent to login', async ({ page }) => {
    await clearSession(page);
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('unknown URLs fall back to login', async ({ page }) => {
    await clearSession(page);
    await page.goto('/nope');
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('admin lands on the dashboard after login', async ({ page }) => {
    await login(page, ADMIN);
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('a customer account cannot open the dashboard', async ({ page }) => {
    await clearSession(page);
    await page.locator('[formcontrolname="emailOrPhone"]').fill(CUSTOMER.email);
    await page.locator('[formcontrolname="password"]').fill(CUSTOMER.password);
    await page.locator('button[type="submit"]').first().click();
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/(forbidden|auth\/login)/);
  });
});
