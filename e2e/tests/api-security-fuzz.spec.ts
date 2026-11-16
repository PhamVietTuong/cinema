import { test, expect } from '@playwright/test';
import { API_URL, CUSTOMER, bearer } from './helpers';

test.use({ baseURL: API_URL });

// Fuzz / negative-input security checks. The contract: hostile input is answered with a 4xx (or a sanitised 200),
// never a 5xx, never a stack trace or SQL error text, and never somebody else's data.
const FUZZ_STRINGS = [
  '',
  ' ',
  "' OR '1'='1' --",
  '1; DROP TABLE Users;--',
  '<script>alert(1)</script>',
  '../../../../windows/win.ini',
  '%00',
  '\u0000',
  'Unicode-ƒüñ-'.repeat(50),
  'A'.repeat(10_000),
  '{{7*7}}',
  '${jndi:ldap://x/a}',
];
const FUZZ_PAGING: unknown[] = [
  { pageIndex: -1, pageSize: 10 },
  { pageIndex: 0, pageSize: 0 },
  { pageIndex: 1, pageSize: 2_147_483_647 },
  { pageIndex: 'x', pageSize: 'y' },
  { pageIndex: 1.5, pageSize: null },
  null,
  [],
];
const LEAK = /(stack trace|System\.|Microsoft\.|SqlException|at Cinema\.|Incorrect syntax)/i;

function expectNoServerFault(status: number, body: string, what: string): void {
  expect(status, `${what} must not be a 5xx`).toBeLessThan(500);
  expect(body, `${what} must not leak internals`).not.toMatch(LEAK);
}

test.describe('API fuzz: public endpoints', () => {
  for (const paging of FUZZ_PAGING) {
    test(`GetMovies survives paging ${JSON.stringify(paging)}`, async ({ request }) => {
      const res = await request.post('/api/Cinema/GetMovies', {
        data: JSON.stringify(paging),
        headers: { 'Content-Type': 'application/json' },
      });
      expectNoServerFault(res.status(), await res.text(), 'GetMovies');
    });
  }

  test('GetMovies survives a body that is not JSON', async ({ request }) => {
    const res = await request.post('/api/Cinema/GetMovies', { data: 'not-json', headers: { 'Content-Type': 'application/json' } });
    expectNoServerFault(res.status(), await res.text(), 'GetMovies non-JSON');
  });

  for (const value of FUZZ_STRINGS) {
    const label = JSON.stringify(value).slice(0, 30);
    test(`search keyword ${label} is handled`, async ({ request }) => {
      const res = await request.post('/api/Cinema/GetMovies', { data: { pageIndex: 1, pageSize: 5, keyword: value, search: value } });
      expectNoServerFault(res.status(), await res.text(), 'GetMovies keyword');
    });
    test(`GetMovieBySlug ${label} is handled`, async ({ request }) => {
      const res = await request.get('/api/Cinema/GetMovieBySlug', { params: { slug: value } });
      expectNoServerFault(res.status(), await res.text(), 'GetMovieBySlug');
    });
  }

  test('malformed GUID query is a 4xx, not a 500', async ({ request }) => {
    const res = await request.get('/api/Cinema/GetMovie', { params: { id: 'not-a-guid' } });
    expectNoServerFault(res.status(), await res.text(), 'GetMovie');
  });

  test('login with fuzzed credentials is refused without a 5xx', async ({ request }) => {
    for (const value of FUZZ_STRINGS.slice(0, 5)) {
      const res = await request.post('/api/Identity/Login', { data: { emailOrPhone: value, password: value } });
      if (res.status() === 429) {
        break; // auth rate limiter engaged: that is the desired behaviour, nothing more to probe
      }
      expectNoServerFault(res.status(), await res.text(), 'Login');
      expect(res.status()).not.toBe(200);
    }
  });
});

test.describe('API fuzz: payment callback (IPN)', () => {
  const cases: Record<string, string>[] = [
    {},
    { vnp_TxnRef: '' },
    { vnp_TxnRef: "'; DROP TABLE Invoices;--", vnp_SecureHash: 'x' },
    { vnp_TxnRef: 'A'.repeat(5000), vnp_SecureHash: 'A'.repeat(5000) },
    { vnp_Amount: '-1', vnp_ResponseCode: '00', vnp_SecureHash: '0' },
  ];
  for (const [i, params] of cases.entries()) {
    test(`garbage IPN #${i} never marks anything paid and never 5xx`, async ({ request }) => {
      const res = await request.get('/api/Payment/PaymentCallback', { params: { provider: 'VNPay', ...params } });
      expect(res.status(), 'IPN must answer 200 with the RspCode shape or a 4xx').toBeLessThan(500);
      if (res.status() === 200) {
        const body = await res.json();
        expect(body).toEqual({ RspCode: expect.any(String), Message: expect.any(String) });
        expect(body.RspCode, 'unsigned callback must not be accepted').not.toBe('00');
      }
    });
  }

  test('unknown provider is rejected without a 5xx', async ({ request }) => {
    const res = await request.get('/api/Payment/PaymentCallback', { params: { provider: "'; --", vnp_SecureHash: 'x' } });
    expect(res.status()).toBeLessThan(500);
  });
});

test.describe('API security: access control (IDOR / privilege)', () => {
  test('a customer cannot read an invoice that is not theirs', async ({ request }) => {
    const headers = await bearer(request, CUSTOMER);
    const res = await request.get('/api/Payment/GetInvoice', { params: { id: '00000000-0000-0000-0000-000000000001' }, headers });
    expect(res.status()).toBeLessThan(500);
    expect(res.status()).not.toBe(200);
  });

  test('a customer cannot call the admin invoice list', async ({ request }) => {
    const headers = await bearer(request, CUSTOMER);
    const res = await request.post('/api/Cinema/GetInvoices', { data: { pageIndex: 1, pageSize: 5 }, headers });
    expect([401, 403]).toContain(res.status());
  });

  for (const token of ['', 'Bearer ', 'Bearer abc.def.ghi', 'Bearer ' + 'A'.repeat(4000), 'Basic YWRtaW46YWRtaW4=']) {
    test(`forged Authorization header ${token.slice(0, 20) || '(empty)'} is 401`, async ({ request }) => {
      const res = await request.post('/api/Identity/GetUsers', {
        data: { pageIndex: 1, pageSize: 5 },
        headers: { Authorization: token },
      });
      expect(res.status()).toBe(401);
    });
  }

  test('responses carry no server version banner', async ({ request }) => {
    const res = await request.get('/health');
    expect(res.headers()['x-powered-by']).toBeUndefined();
    expect(res.headers()['server'] ?? '').not.toMatch(/\d+\.\d+/);
  });
});
