import { test, expect } from '@playwright/test';
import { USER_URL, expectRouteRenders } from './helpers';

test.use({ baseURL: USER_URL });

test.describe('CinemaUser: public discovery pages', () => {
  for (const route of ['/', '/movies', '/theaters', '/promotions', '/membership']) {
    test(`${route} renders without errors`, async ({ page }) => {
      await expectRouteRenders(page, route);
    });
  }

  test('unknown URLs fall back to home', async ({ page }) => {
    await page.goto('/this-route-does-not-exist');
    await expect(page).toHaveURL(/\/$/);
  });
});

test.describe('CinemaUser: movies browse and detail', () => {
  test('movie list shows the shared filter bar', async ({ page }) => {
    await page.goto('/movies');
    await expect(page.locator('cl-filter-bar').first()).toBeVisible();
  });

  test('opening a movie navigates to its slug detail page', async ({ page }) => {
    await page.goto('/movies');
    const link = page.locator('a[href^="/movies/"]').first();
    test.skip((await link.count()) === 0, 'No movies seeded');
    await link.click();
    await expect(page).toHaveURL(/\/movies\/[^/]+$/);
    await expect(page.locator('h1').first()).toBeVisible();
  });
});

test.describe('CinemaUser: support chatbot', () => {
  test('chatbot is mounted on the public site', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('app-chatbot, cinema-chatbot, [class*="chatbot"]').first()).toBeAttached();
  });
});
