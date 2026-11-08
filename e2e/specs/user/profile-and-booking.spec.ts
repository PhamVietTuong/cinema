import { test, expect } from '@playwright/test';
import { CUSTOMER } from '../../fixtures/personas';
import { loginAs } from '../../helpers/auth';

test.describe('CinemaUser: signed-in customer', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, CUSTOMER);
  });

  test('profile page opens', async ({ page }) => {
    await page.goto('/profile');
    await expect(page).toHaveURL(/\/profile/);
    await expect(page.locator('body')).toContainText(/\S/);
  });

  test('profile survives a reload (session persists)', async ({ page }) => {
    await page.goto('/profile');
    await page.reload();
    await expect(page).toHaveURL(/\/profile/);
  });

  test('booking seat map opens from a movie showtime', async ({ page }) => {
    await page.goto('/movies');
    const movie = page.locator('a[href^="/movies/"]').first();
    test.skip((await movie.count()) === 0, 'No movies seeded');
    await movie.click();
    const showtime = page.locator('a[href*="/booking/seats"]').first();
    test.skip((await showtime.count()) === 0, 'No upcoming showtime seeded');
    await showtime.click();
    await expect(page).toHaveURL(/\/booking\/seats/);
    await expect(page.locator('cl-seat-map')).toBeVisible();
  });

  test('removing the token sends the next protected visit to login', async ({ page }) => {
    await page.evaluate(() => localStorage.removeItem('cinema_token'));
    await page.goto('/profile');
    await expect(page).toHaveURL(/\/auth\/login/);
  });
});
