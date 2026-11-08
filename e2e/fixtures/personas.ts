/**
 * Accounts seeded by `dotnet run --project Cinema/2-Business/Cinema.Business.Tests`.
 * Override through env vars in CI; the defaults are local-dev seed credentials only.
 */
export interface Persona {
  email: string;
  password: string;
}

export const ADMIN: Persona = {
  email: process.env.E2E_ADMIN_EMAIL || 'admin@cinema.vn',
  password: process.env.E2E_ADMIN_PASSWORD || 'Admin@123',
};

export const CUSTOMER: Persona = {
  email: process.env.E2E_USER_EMAIL || 'user@cinema.vn',
  password: process.env.E2E_USER_PASSWORD || 'User@123',
};

/** Optional: the seed does not create a TheaterStaff account; staff role specs skip without one. */
export const STAFF: Persona | null = process.env.E2E_STAFF_EMAIL
  ? { email: process.env.E2E_STAFF_EMAIL, password: process.env.E2E_STAFF_PASSWORD || '' }
  : null;
