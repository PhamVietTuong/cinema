import { test, expect } from '@playwright/test';
import { API_URL, SESSION, USER_URL, blockExternalMedia, dismissSplashBanner, dropToken, login } from './helpers';

test.use({ baseURL: USER_URL });

// Seeded posters and theater images live on third-party hosts; never wait for them (see blockExternalMedia).
test.beforeEach(async ({ page }) => {
  await blockExternalMedia(page);
});

test.describe('CinemaUser: signed-in customer', () => {
  test.use({ storageState: SESSION.user });

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

  test('booking seat map opens from a movie showtime', { tag: ['@BR-SEAT-01'] }, async ({ page, request }) => {
    // Pick a movie that really has an upcoming showtime: most seeded movies have none, so "the first movie" is not
    // a stable choice. Never skip: no showtime means the seed is stale, and a skipped booking test hides a broken flow.
    const res = await request.post(`${API_URL}/api/Cinema/SearchShowTimes`, { data: { pageIndex: 1, pageSize: 1 } });
    expect(res.ok(), 'SearchShowTimes should answer').toBeTruthy();
    const upcoming = (await res.json()).results as { movieSlug: string }[];
    expect(
      upcoming.length,
      'No upcoming showtime: run e2e/sql/seed-future-showtime.sql against the Cinema database (CI does this after insert_db).'
    ).toBeGreaterThan(0);

    await page.goto(`/movies/${upcoming[0].movieSlug}`);
    await dismissSplashBanner(page);
    // The movie page embeds the seat panel inline: pick a date that has a slot, then the slot.
    const slots = page.locator('button.time-slot');
    const dateTabs = page.locator('button.date-tab');
    await expect(dateTabs.first(), 'The movie page should show its date strip').toBeVisible({ timeout: 15_000 });
    for (let i = 0; i < (await dateTabs.count()); i++) {
      await dateTabs.nth(i).click();
      const hasSlot = await slots.first().waitFor({ state: 'visible', timeout: 2_000 }).then(() => true, () => false);
      if (hasSlot) {
        break;
      }
    }
    await expect(slots.first(), 'The movie page should list its upcoming showtime').toBeVisible();
    await slots.first().click();
    await expect(page.locator('cl-seat-map')).toBeVisible();
  });

  test('removing the token sends the next protected visit to login', async ({ page }) => {
    await dropToken(page);
    await page.goto('/profile');
    await expect(page).toHaveURL(/\/auth\/login/);
  });
});
