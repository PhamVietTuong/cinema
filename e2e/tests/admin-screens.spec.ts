import { test, expect } from '@playwright/test';
import { ADMIN_URL, SESSION, expectRouteRenders } from './helpers';

test.use({ baseURL: ADMIN_URL });

/** Every routed admin screen, grouped as in the three lazy modules of app.routes.ts. */
const SCREENS: Record<string, string[]> = {
  overview: ['/dashboard', '/reports', '/movies', '/theaters', '/showtimes', '/users', '/profile'],
  catalog: [
    '/age-restrictions', '/movie-types', '/memberships', '/user-types',
    '/holidays', '/news', '/templates', '/promotion-campaigns',
  ],
  operations: [
    '/invoices', '/comments', '/audit-log',
    '/promotion-programs', '/splash-banners',
  ],
};

test.describe('CinemaAdmin: every screen loads for an admin', () => {
  test.use({ storageState: SESSION.admin });

  for (const [group, routes] of Object.entries(SCREENS)) {
    for (const route of routes) {
      test(`${group}: ${route}`, async ({ page }) => {
        await expectRouteRenders(page, route);
      });
    }
  }
});

test.describe('CinemaAdmin: theater management', () => {
  test.use({ storageState: SESSION.admin });

  test('theater list opens a detail page with tabs', async ({ page }) => {
    await page.goto('/theaters');
    const link = page.locator('a[href^="/theaters/"]').first();
    test.skip((await link.count()) === 0, 'No theater detail links (or none seeded)');
    await link.click();
    await expect(page).toHaveURL(/\/theaters\/[^/]+/);
    await expect(page.locator('[role="tab"]').first()).toBeVisible();
  });

  test('movie list opens the cast page with a save bar', async ({ page }) => {
    await page.goto('/movies');
    const link = page.locator('a[href^="/movies/"][href$="/cast"]').first();
    await link.waitFor({ state: 'attached', timeout: 10000 }).catch(() => undefined);
    test.skip((await link.count()) === 0, 'No movies seeded');
    const href = await link.getAttribute('href');
    await link.click();
    await expect(page).toHaveURL(new RegExp(`${href}$`));
    await expect(page.locator('.cast-savebar')).toBeVisible();
  });
});
