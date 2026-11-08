import { APIRequestContext } from '@playwright/test';
import { Persona } from '../fixtures/personas';

export const PAGING = { pageIndex: 1, pageSize: 5 };

/** Logs in over the API and returns an Authorization header. */
export async function bearer(request: APIRequestContext, persona: Persona): Promise<Record<string, string>> {
  const res = await request.post('/api/Identity/Login', {
    data: { emailOrPhone: persona.email, password: persona.password },
  });
  if (!res.ok()) {
    throw new Error(`Login failed for ${persona.email}: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  const token = body.token ?? body.accessToken ?? body.data?.token ?? body.data?.accessToken;
  if (!token) {
    throw new Error(`Login response has no token field: ${JSON.stringify(Object.keys(body))}`);
  }
  return { Authorization: `Bearer ${token}` };
}
