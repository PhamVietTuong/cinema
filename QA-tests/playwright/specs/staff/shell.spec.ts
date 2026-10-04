import { test, expect } from '@playwright/test';
import { PERSONAS } from '../../fixtures/personas';
import { loginAs } from '../../utils/login.helper';

/**
 * Smoke — CinemaStaff shell (:4203).
 * Runs under the `cinema-staff-chromium` project (testMatch on specs/staff/).
 */
test.describe('Smoke — CinemaStaff shell + role gating', () => {

  test('S00.1 — Staff logs in and sees the shared shell on /home', async ({ page }) => {
    await loginAs(page, PERSONAS.staff);
    await expect(page).toHaveURL(/\/home/);
    await expect(page.locator('cl-app-shell .sidebar')).toBeVisible();
    await expect(page.locator('cl-app-shell .brand-text')).toContainText('STAFF');
  });

  test('S00.2 — Admin sees the theater picker in the topbar', async ({ page }) => {
    await loginAs(page, PERSONAS.admin);
    await expect(page.locator('cl-app-shell .topbar .theater-picker select')).toBeVisible();
  });

  test('S00.3 — Staff has no theater picker (pinned to own theater)', async ({ page }) => {
    await loginAs(page, PERSONAS.staff);
    await expect(page.locator('cl-app-shell .topbar .theater-picker')).toHaveCount(0);
  });

  test('S00.4 — Standard customer is refused at the staff login', async ({ page }) => {
    await page.goto('/auth/login');
    await page.waitForSelector('[formcontrolname="emailOrPhone"]', { timeout: 15000 });
    await page.fill('[formcontrolname="emailOrPhone"]', PERSONAS.user.email);
    await page.fill('[formcontrolname="password"]', PERSONAS.user.password);
    await page.click('button:has-text("Đăng Nhập")');
    // Bounced to the login page or the forbidden page, and never shown the shell.
    await page.waitForURL(/\/(auth\/login|forbidden)/, { timeout: 15000 });
    await expect(page.locator('cl-app-shell')).toHaveCount(0);
  });

  test('S00.5 — Language switch flips the sidebar labels (vi -> en)', async ({ page }) => {
    await loginAs(page, PERSONAS.staff);
    await page.locator('cl-language-switcher button').click();
    await page.getByRole('menuitem', { name: 'English' }).click();
    await expect(page.locator('cl-app-shell .nav-item').first()).toContainText('Home');
  });
});
