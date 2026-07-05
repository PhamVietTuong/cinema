import { test, expect } from '@playwright/test';
import { PERSONAS } from '../../fixtures/personas';
import { loginAs } from '../../utils/login.helper';

/**
 * Booking golden path on CinemaUser (:4202).
 *
 * The seat-selection route needs a real seeded showtime + room:
 *   /booking/seats?showTimeId=<id>&roomId=<id>
 * Provide them via env so this runs against your seeded DB:
 *   CINEMA_SHOWTIME_ID=... CINEMA_ROOM_ID=... npm run test:booking
 * Without them, the seat-grid steps are skipped (login + guard still assert).
 */
const SHOWTIME_ID = process.env.CINEMA_SHOWTIME_ID;
const ROOM_ID = process.env.CINEMA_ROOM_ID;
const haveSeed = !!(SHOWTIME_ID && ROOM_ID);

test.describe('Booking — golden path', () => {

  test('B01 — user reaches the seat grid for a showtime', async ({ page }) => {
    test.skip(!haveSeed, 'Set CINEMA_SHOWTIME_ID + CINEMA_ROOM_ID to run against seeded data');
    await loginAs(page, PERSONAS.user);
    await page.goto(`/booking/seats?showTimeId=${SHOWTIME_ID}&roomId=${ROOM_ID}`);
    await expect(page.locator('.seat-grid')).toBeVisible();
    // Seats render with their state classes.
    await expect(page.locator('.seat').first()).toBeVisible();
  });

  test('B02 — select available seats and reach confirmation', async ({ page }) => {
    test.skip(!haveSeed, 'Set CINEMA_SHOWTIME_ID + CINEMA_ROOM_ID to run against seeded data');
    await loginAs(page, PERSONAS.user);
    await page.goto(`/booking/seats?showTimeId=${SHOWTIME_ID}&roomId=${ROOM_ID}`);

    const available = page.locator('.seat.available');
    await expect(available.first()).toBeVisible();
    const toPick = Math.min(2, await available.count());
    for (let i = 0; i < toPick; i++) {
      await available.nth(i).click();
    }
    // Picked seats flip to the selected state.
    await expect(page.locator('.seat.selected')).toHaveCount(toPick);

    await page.click('.confirm-btn:has-text("XÁC NHẬN ĐẶT VÉ")');
    await expect(page).toHaveURL(/\/booking\/confirmation/);
  });

  test('B03 — occupied seats are not selectable', async ({ page }) => {
    test.skip(!haveSeed, 'Set CINEMA_SHOWTIME_ID + CINEMA_ROOM_ID to run against seeded data');
    await loginAs(page, PERSONAS.user);
    await page.goto(`/booking/seats?showTimeId=${SHOWTIME_ID}&roomId=${ROOM_ID}`);

    const occupied = page.locator('.seat.occupied');
    if (await occupied.count() === 0) test.skip(true, 'No occupied seats in this showtime to assert against');
    const before = await page.locator('.seat.selected').count();
    await occupied.first().click({ force: true });
    // Clicking an occupied seat must not add it to the selection.
    await expect(page.locator('.seat.selected')).toHaveCount(before);
  });
});
