import { test, expect } from '@playwright/test';
import { ADMIN, CUSTOMER } from '../../fixtures/personas';
import { PAGING, bearer } from '../../helpers/api';

test.describe('API: authentication and authorization', () => {
  test('login with valid credentials returns a token', async ({ request }) => {
    const res = await request.post('/api/Identity/Login', {
      data: { emailOrPhone: CUSTOMER.email, password: CUSTOMER.password },
    });
    expect(res.status()).toBe(200);
    expect(JSON.stringify(await res.json())).toMatch(/token/i);
  });

  test('login with a wrong password is refused', async ({ request }) => {
    const res = await request.post('/api/Identity/Login', {
      data: { emailOrPhone: CUSTOMER.email, password: 'definitely-wrong-password' },
    });
    expect([400, 401]).toContain(res.status());
  });

  test('login with an empty body is a validation error', async ({ request }) => {
    const res = await request.post('/api/Identity/Login', { data: {} });
    expect(res.status()).toBe(400);
  });

  test('admin endpoints require a token', async ({ request }) => {
    const res = await request.post('/api/Identity/GetUsers', { data: PAGING });
    expect(res.status()).toBe(401);
  });

  test('admin endpoints reject a customer token', async ({ request }) => {
    const headers = await bearer(request, CUSTOMER);
    const res = await request.post('/api/Identity/GetUsers', { data: PAGING, headers });
    expect(res.status()).toBe(403);
  });

  test('admin can list users and invoices', async ({ request }) => {
    const headers = await bearer(request, ADMIN);
    const users = await request.post('/api/Identity/GetUsers', { data: PAGING, headers });
    expect(users.status()).toBe(200);
    const invoices = await request.post('/api/Payment/GetInvoices', { data: PAGING, headers });
    expect(invoices.status()).toBe(200);
  });
});
