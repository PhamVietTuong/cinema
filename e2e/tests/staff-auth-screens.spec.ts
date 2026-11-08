import { test, expect } from '@playwright/test';
import { ADMIN, ANONYMOUS, CUSTOMER, SESSION, STAFF, STAFF_URL, clearSession, expectRouteRenders, login } from './helpers';

test.use({ baseURL: STAFF_URL });

test.describe('CinemaStaff: authentication and guards', () => {
  test.use({ storageState: ANONYMOUS });
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
    await login(page, ADMIN);
    await expect(page).toHaveURL(/\/home/);
  });

  test('theater staff account lands on /home', async ({ page }) => {
    await login(page, STAFF);
    await expect(page).toHaveURL(/\/home/);
  });
});

test.describe('CinemaStaff: every screen loads for an admin', () => {
  test.use({ storageState: SESSION.staff });

  const screens = ['/home', '/profile', '/gate', '/pos', '/schedule', '/incidents', '/reports', '/audit-log'];
  for (const route of screens) {
    test(route, async ({ page }) => {
      await expectRouteRenders(page, route);
    });
  }
});
