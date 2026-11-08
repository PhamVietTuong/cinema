import { test, expect } from '@playwright/test';
import { CUSTOMER } from '../../fixtures/personas';
import { clearSession, getToken, loginAs } from '../../helpers/auth';

test.describe('CinemaUser: authentication', () => {
  test.beforeEach(async ({ page }) => {
    await clearSession(page);
  });

  test('login page exposes the credentials form and auth links', async ({ page }) => {
    await page.goto('/auth/login');
    await expect(page.locator('[formcontrolname="emailOrPhone"]')).toBeVisible();
    await expect(page.locator('[formcontrolname="password"]')).toBeVisible();
    await expect(page.locator('a[href="/auth/register"]').first()).toBeVisible();
    await expect(page.locator('a[href="/auth/forgot-password"]')).toBeVisible();
  });

  test('/login alias redirects to /auth/login', async ({ page }) => {
    await page.goto('/login');
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('submit stays disabled until the form is valid', async ({ page }) => {
    await page.goto('/auth/login');
    await expect(page.locator('button[type="submit"]').first()).toBeDisabled();
  });

  test('valid credentials sign the customer in', async ({ page }) => {
    await loginAs(page, CUSTOMER);
    expect(await getToken(page)).toBeTruthy();
  });

  test('wrong password keeps the user on the login page without a token', async ({ page }) => {
    await page.goto('/auth/login');
    await page.locator('[formcontrolname="emailOrPhone"]').fill(CUSTOMER.email);
    await page.locator('[formcontrolname="password"]').fill('wrong-password-123');
    await page.locator('button[type="submit"]').first().click();
    await expect(page).toHaveURL(/\/auth\/login/);
    expect(await getToken(page)).toBeFalsy();
  });

  for (const route of ['register', 'forgot-password', 'reset-password', 'verify-email']) {
    test(`/auth/${route} renders`, async ({ page }) => {
      await page.goto(`/auth/${route}`);
      await expect(page).toHaveURL(new RegExp(`/auth/${route}`));
      await expect(page.locator('form, input').first()).toBeVisible();
    });
  }
});

test.describe('CinemaUser: route guards', () => {
  for (const route of ['/profile', '/booking/seats', '/booking/checkout']) {
    test(`anonymous visit to ${route} is sent to login`, async ({ page }) => {
      await clearSession(page);
      await page.goto(route);
      await expect(page).toHaveURL(/\/auth\/login/);
    });
  }
});
