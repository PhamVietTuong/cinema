import { Page, expect } from '@playwright/test';

/**
 * Opens a route and asserts it rendered: stayed on the path, no uncaught page errors,
 * and the app root has content. Used for table-driven "every screen loads" specs.
 */
export async function expectRouteRenders(page: Page, path: string): Promise<void> {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));

  await page.goto(path);
  await page.waitForLoadState('networkidle');

  await expect(page).toHaveURL(new RegExp(`${path.replace(/[/]/g, '\/')}(\?.*)?$`));
  await expect(page.locator('body')).not.toBeEmpty();
  expect(errors, `uncaught errors on ${path}`).toEqual([]);
}
