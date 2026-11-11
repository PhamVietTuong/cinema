import { createHmac } from 'node:crypto';
import { test, expect } from '@playwright/test';
import { API_URL } from './helpers';

test.use({ baseURL: API_URL });

// The API must run with Payments:VnPay:TmnCode and HashSecret set, and E2E_VNPAY_HASH_SECRET must hold the same
// secret (CI does both). Without a configured VNPay gateway the controller does not take the VNPay path at all, so
// a test that "passed" would prove nothing: it is skipped locally when the secret is absent and a failure in CI.
const HASH_SECRET = process.env.E2E_VNPAY_HASH_SECRET ?? '';
const IN_CI = !!process.env.CI;

test.beforeEach(() => {
  if (!HASH_SECRET) {
    if (IN_CI) {
      throw new Error('E2E_VNPAY_HASH_SECRET is not set: the CI e2e job must configure VNPay (see .github/workflows/ci.yml).');
    }
    test.skip(true, 'Set E2E_VNPAY_HASH_SECRET (and run the API with the same Payments:VnPay:HashSecret) to run this.');
  }
});

// Mirrors System.Net.WebUtility.UrlEncode as used by VnPayGateway: space is '+', ' and ~ are percent-encoded
// (uppercase hex) and ! * ( ) stay literal.
function urlEncode(value: string): string {
  return encodeURIComponent(value)
    .replace(/%20/g, '+')
    .replace(/['~]/g, (c) => '%' + c.charCodeAt(0).toString(16).toUpperCase());
}

function signed(params: Record<string, string>): Record<string, string> {
  const data = Object.keys(params)
    .sort()
    .map((k) => `${urlEncode(k)}=${urlEncode(params[k])}`)
    .join('&');
  const hash = createHmac('sha512', HASH_SECRET).update(data, 'utf8').digest('hex');
  return { ...params, vnp_SecureHash: hash };
}

test.describe('API: payment callbacks are signature-authoritative', () => {
  test('VNPay IPN with a bad signature is answered 200 with RspCode 97', { tag: ['@BR-PAY-02'] }, async ({ request }) => {
    const res = await request.get('/api/Payment/PaymentCallback', {
      params: { provider: 'VNPay', vnp_TxnRef: 'e2e-fake', vnp_ResponseCode: '00', vnp_SecureHash: 'bad' },
    });
    expect(res.status()).toBe(200);
    expect(await res.json()).toEqual({ RspCode: '97', Message: expect.any(String) });
  });

  test('VNPay IPN with a valid signature for an unknown order is answered 200 with RspCode 01', { tag: ['@BR-PAY-02'] }, async ({ request }) => {
    const params = signed({
      // 32-char invoice id (all zeros: no such invoice) followed by a per-attempt suffix, as VnPayGateway builds it.
      vnp_TxnRef: '0'.repeat(32) + '120000000',
      vnp_Amount: '10000000',
      vnp_ResponseCode: '00',
      vnp_TransactionStatus: '00',
      vnp_TransactionNo: 'e2e-unknown-order',
    });
    const res = await request.get('/api/Payment/PaymentCallback', { params: { provider: 'VNPay', ...params } });
    expect(res.status()).toBe(200);
    expect(await res.json()).toEqual({ RspCode: '01', Message: expect.any(String) });
  });
});
