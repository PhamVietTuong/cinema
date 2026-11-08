import { test, expect } from '@playwright/test';
import { ADMIN, CUSTOMER, STAFF } from '../../fixtures/personas';
import { clearSession, loginAs } from '../../helpers/auth';
import { expectRouteRenders } from '../../helpers/page-health';

test.describe('CinemaStaff: authentication and guards', () => {
  test('anonymous visitors are sent to login', async ({ page }) => {
    await clearSession(page);
    await page.goto('/home');
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('/login alias redirects to /auth/login', async ({ page }) => {
    await page.goto('/login');
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('a customer account cannot use the staff app', async ({ page }) => {
    await clearSession(page);
    await page.locator('[formcontrolname="emailOrPhone"]').fill(CUSTOMER.email);
    await page.locator('[formcontrolname="password"]').fill(CUSTOMER.password);
    await page.locator('button[type="submit"]').first().click();
    await page.goto('/home');
    await expect(page).toHaveURL(/\/(forbidden|auth\/login)/);
  });

  test('admin lands on /home', async ({ page }) => {
    await loginAs(page, ADMIN);
    await expect(page).toHaveURL(/\/home/);
  });

  test('theater staff account lands on /home', async ({ page }) => {
    test.skip(!STAFF, 'Set E2E_STAFF_EMAIL / E2E_STAFF_PASSWORD to run');
    await loginAs(page, STAFF!);
    await expect(page).toHaveURL(/\/home/);
  });
});

test.describe('CinemaStaff: every screen loads for an admin', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ADMIN);
  });

  const screens = ['/home', '/profile', '/gate', '/pos', '/schedule', '/incidents', '/reports', '/audit-log'];
  for (const route of screens) {
    test(route, async ({ page }) => {
      await expectRouteRenders(page, route);
    });
  }
});
