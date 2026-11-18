import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';
import { ADMIN_URL, STAFF_URL, USER_URL, ANONYMOUS, blockExternalMedia } from './helpers';

// Usability / accessibility: axe-core (WCAG 2.1 A + AA) on the anonymous pages of each app. Only `serious` and
// `critical` violations fail the run; the full list is attached to each test so the backlog stays visible.
// Colour contrast is excluded from the gate until the design tokens are audited.
test.use({ storageState: ANONYMOUS });

// Known backlog found by the first axe run (2026-11-16). Ratchet: these rule ids are tolerated, anything else
// serious/critical fails. Remove an id from this list when its violations are fixed; never add one.
//   link-name                    - icon-only links without an accessible name (CinemaUser home, movies)
//   button-name                  - icon-only button without a label (CinemaUser login)
//   label-content-name-mismatch  - aria-label that does not contain the visible text (all three apps)
const KNOWN_BACKLOG = new Set(['link-name', 'button-name', 'label-content-name-mismatch']);

const pages = [
  { app: 'CinemaUser', url: USER_URL, path: '/' },
  { app: 'CinemaUser', url: USER_URL, path: '/movies' },
  { app: 'CinemaUser', url: USER_URL, path: '/theaters' },
  { app: 'CinemaUser', url: USER_URL, path: '/auth/login' },
  { app: 'CinemaAdmin', url: ADMIN_URL, path: '/auth/login' },
  { app: 'CinemaStaff', url: STAFF_URL, path: '/auth/login' },
];

for (const p of pages) {
  test(`${p.app} ${p.path} has no serious accessibility violations @a11y`, async ({ page }, info) => {
    await blockExternalMedia(page);
    await page.goto(`${p.url}${p.path}`);
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .disableRules(['color-contrast'])
      .analyze();

    await info.attach('axe-violations.json', { body: JSON.stringify(results.violations, null, 2), contentType: 'application/json' });
    const blocking = results.violations.filter((v) => (v.impact === 'serious' || v.impact === 'critical') && !KNOWN_BACKLOG.has(v.id));
    const summary = blocking.map((v) => `${v.id} (${v.impact}): ${v.nodes.length} node(s) - ${v.help}`);
    expect(summary, `blocking a11y violations on ${p.app}${p.path}`).toEqual([]);
  });
}
