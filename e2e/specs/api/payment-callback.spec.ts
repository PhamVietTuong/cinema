import { test, expect } from '@playwright/test';

test.describe('API: payment callbacks are signature-authoritative', () => {
  test('VNPay IPN with a bad signature never reports success', async ({ request }) => {
    const res = await request.get('/api/Payment/PaymentCallback', {
      params: { provider: 'VnPay', vnp_TxnRef: 'e2e-fake', vnp_ResponseCode: '00', vnp_SecureHash: 'bad' },
    });
    // Contract: HTTP 200 with the {"RspCode","Message"} shape, but never RspCode "00".
    if (res.status() === 200) {
      const body = await res.json();
      expect(body).toEqual(expect.objectContaining({ RspCode: expect.any(String), Message: expect.any(String) }));
      expect(body.RspCode).not.toBe('00');
    } else {
      expect([400, 404]).toContain(res.status());
    }
  });
});
