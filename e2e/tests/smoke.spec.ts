import { test, expect } from '@playwright/test';
import { ADMIN_URL, API_URL, STAFF_URL, USER_URL, ANONYMOUS, blockExternalMedia } from './helpers';

// Smoke: the shortest path that proves a deployment is alive — API up, every app boots and reaches its login or
// home screen. Runs on every compat project (`npm run test:compat`). Sanity runs are done by
// tagging a spec `@sanity` (`npm run test:sanity`): a narrow re-check of one area after a fix.
test.use({ storageState: ANONYMOUS });

test.describe('Smoke: stack is alive', () => {
  test('API liveness and public catalog @smoke', async ({ request }) => {
    expect((await request.get(`${API_URL}/health`)).status()).toBe(200);
    const res = await request.post(`${API_URL}/api/Cinema/GetNowShowingMovies`, { data: { pageIndex: 1, pageSize: 5 } });
    expect(res.status()).toBe(200);
  });

  const apps = [
    { name: 'CinemaUser', url: USER_URL, path: '/' },
    { name: 'CinemaUser login', url: USER_URL, path: '/auth/login' },
    { name: 'CinemaAdmin login', url: ADMIN_URL, path: '/auth/login' },
    { name: 'CinemaStaff login', url: STAFF_URL, path: '/auth/login' },
  ];
  for (const app of apps) {
    test(`${app.name} boots without uncaught errors @smoke`, async ({ page }) => {
      const errors: string[] = [];
      page.on('pageerror', (e) => errors.push(e.message));
      await blockExternalMedia(page);
      await page.goto(`${app.url}${app.path}`);
      await expect(page.locator('body')).not.toBeEmpty();
      await expect(page.locator('app-root').first()).toBeVisible();
      expect(errors, `uncaught errors on ${app.name}`).toEqual([]);
    });
  }

  test('customer login form is usable on this device @smoke', async ({ page }) => {
    await page.goto(`${USER_URL}/auth/login`);
    await expect(page.locator('[formcontrolname="emailOrPhone"]')).toBeVisible();
    await expect(page.locator('[formcontrolname="password"]')).toBeVisible();
  });

  test('no page-level horizontal scroll on the home page @smoke', async ({ page }) => {
    await blockExternalMedia(page);
    await page.goto(`${USER_URL}/`);
    await page.waitForLoadState('domcontentloaded');
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    expect(overflow).toBeLessThanOrEqual(1);
  });
});
