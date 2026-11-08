import { test, expect } from '@playwright/test';
import { ADMIN } from '../../fixtures/personas';
import { loginAs } from '../../helpers/auth';
import { expectRouteRenders } from '../../helpers/page-health';

/** Every routed admin screen, grouped as in the three lazy modules of app.routes.ts. */
const SCREENS: Record<string, string[]> = {
  overview: ['/dashboard', '/reports', '/movies', '/theaters', '/showtimes', '/users', '/profile'],
  catalog: [
    '/age-restrictions', '/movie-types', '/memberships', '/user-types',
    '/holidays', '/news', '/templates', '/promotion-campaigns',
  ],
  operations: [
    '/invoices', '/comments', '/gift-cards', '/audit-log',
    '/loyalty-settings', '/promotion-programs', '/splash-banners',
  ],
};

test.describe('CinemaAdmin: every screen loads for an admin', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ADMIN);
  });

  for (const [group, routes] of Object.entries(SCREENS)) {
    for (const route of routes) {
      test(`${group}: ${route}`, async ({ page }) => {
        await expectRouteRenders(page, route);
      });
    }
  }
});

test.describe('CinemaAdmin: theater management', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ADMIN);
  });

  test('theater list opens a detail page with tabs', async ({ page }) => {
    await page.goto('/theaters');
    const link = page.locator('a[href^="/theaters/"]').first();
    test.skip((await link.count()) === 0, 'No theater detail links (or none seeded)');
    await link.click();
    await expect(page).toHaveURL(/\/theaters\/[^/]+/);
    await expect(page.locator('[role="tab"]').first()).toBeVisible();
  });
});
