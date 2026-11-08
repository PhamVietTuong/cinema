import { test, expect } from '@playwright/test';
import { ADMIN, CUSTOMER } from '../../fixtures/personas';
import { clearSession, loginAs } from '../../helpers/auth';

test.describe('CinemaAdmin: authentication and guards', () => {
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
    await loginAs(page, ADMIN);
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
