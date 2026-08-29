import {
  selectAuthError,
  selectAuthLoading,
  selectAwaitingTwoFactor,
  selectCurrentUser,
  selectIsAdmin,
  selectIsAuthenticated,
  selectToken,
} from './auth.selectors';
import { AuthState, initialAuthState } from './auth.state';
import { UserProfile } from '../../models/auth.models';

const customer: UserProfile = {
  id: 'u1',
  name: 'Nguyen Van A',
  email: 'a@cinema.vn',
  phone: '0901234567',
  userTypeId: 2,
  userTypeName: 'Customer',
  points: 120,
};

const admin: UserProfile = { ...customer, id: 'u2', userTypeId: 1, userTypeName: 'Admin' };

const stateWith = (overrides: Partial<AuthState>): AuthState => ({ ...initialAuthState, ...overrides });

describe('auth selectors', () => {
  it('selectIsAuthenticated is false without a token', () => {
    expect(selectIsAuthenticated.projector(null)).toBe(false);
    expect(selectIsAuthenticated.projector('')).toBe(false);
  });

  it('selectIsAuthenticated is true with a token', () => {
    expect(selectIsAuthenticated.projector('tok')).toBe(true);
  });

  it('selectIsAdmin is true only for the Admin user type', () => {
    expect(selectIsAdmin.projector(admin)).toBe(true);
    expect(selectIsAdmin.projector(customer)).toBe(false);
    expect(selectIsAdmin.projector(null)).toBe(false);
  });

  it('reads user, token, loading, error and 2FA flags off the feature state', () => {
    const state = stateWith({
      user: customer,
      token: 'tok',
      loading: true,
      error: 'boom',
      awaitingTwoFactor: true,
    });

    expect(selectCurrentUser.projector(state)).toEqual(customer);
    expect(selectToken.projector(state)).toBe('tok');
    expect(selectAuthLoading.projector(state)).toBe(true);
    expect(selectAuthError.projector(state)).toBe('boom');
    expect(selectAwaitingTwoFactor.projector(state)).toBe(true);
  });
});
