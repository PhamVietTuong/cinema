import { Page, expect } from '@playwright/test';
import { Persona } from '../fixtures/personas';

/**
 * Login helper for CinemaUser (:4202) and CinemaAdmin (:4201).
 *
 * Both apps share the same login template contract:
 *   - route /auth/login (alias /login)
 *   - formControlName="emailOrPhone" + formControlName="password"
 *   - submit button text "Đăng Nhập"
 *   - on success the JWT lands in localStorage under 'cinema_token'
 */
export async function loginAs(page: Page, persona: Persona) {
  await page.goto('/auth/login');
  await page.waitForSelector('[formcontrolname="emailOrPhone"]', { timeout: 15000 });

  await page.fill('[formcontrolname="emailOrPhone"]', persona.email);
  await page.fill('[formcontrolname="password"]', persona.password);
  await page.click('button:has-text("Đăng Nhập")');

  // Auth effect navigates away from /auth/login on success.
  await page.waitForURL((url) => !url.pathname.includes('/auth/login') && !url.pathname.endsWith('/login'), {
    timeout: 15000,
  });

  // JWT persisted → confirms a real authenticated session, not just a redirect.
  const token = await page.evaluate(() => localStorage.getItem('cinema_token'));
  expect(token, 'cinema_token should be set in localStorage after login').toBeTruthy();
}

export async function logout(page: Page) {
  await page.evaluate(() => {
    localStorage.removeItem('cinema_token');
    localStorage.removeItem('cinema_user');
  });
  await page.goto('/auth/login');
  await page.waitForURL((url) => url.pathname.includes('/login'), { timeout: 10000 });
}
