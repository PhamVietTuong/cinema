/**
 * Test personas — seeded by `dotnet run --project Cinema/2-Business/Cinema.Business.Tests`.
 * Credentials are LOCAL DEV ONLY (see Cinema.Business.Tests/Program.cs).
 */
export interface Persona {
  email: string;
  password: string;
  role: 'Admin' | 'User';
  label: string;
}

export const PERSONAS: Record<string, Persona> = {
  admin: {
    email: 'admin@cinema.vn',
    password: 'Admin@123',
    role: 'Admin',
    label: 'Seeded admin account',
  },
  user: {
    email: 'user@cinema.vn',
    password: 'User@123',
    role: 'User',
    label: 'Seeded standard user account',
  },
};
