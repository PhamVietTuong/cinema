import { authReducer } from './auth.reducer';
import { initialAuthState, AuthState } from './auth.state';
import { TokenStorage } from './token-storage';
import * as AuthActions from './auth.actions';
import { AuthResponse, UserProfile } from '../../models/auth.models';

const user: UserProfile = {
  id: 'u1',
  name: 'Nguyen Van A',
  email: 'a@cinema.vn',
  phone: '0901234567',
  userTypeId: 2,
  userTypeName: 'Customer',
  points: 0,
};

const response: AuthResponse = { token: 'tok', expiresAt: '2026-12-31T00:00:00Z', user };

describe('authReducer', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it('login sets loading and clears any previous error', () => {
    const from: AuthState = { ...initialAuthState, error: 'previous failure' };

    const state = authReducer(from, AuthActions.login({ request: { emailOrPhone: 'a', password: 'b' } }));

    expect(state.loading).toBe(true);
    expect(state.error).toBeNull();
  });

  it('loginSuccess stores the token and user and stops loading', () => {
    const state = authReducer(initialAuthState, AuthActions.loginSuccess({ response }));

    expect(state.token).toBe('tok');
    expect(state.user).toEqual(user);
    expect(state.loading).toBe(false);
    expect(state.error).toBeNull();
  });

  it('loginFailure records the error without setting a token', () => {
    const state = authReducer(initialAuthState, AuthActions.loginFailure({ error: 'Invalid credentials.' }));

    expect(state.error).toBe('Invalid credentials.');
    expect(state.token).toBeNull();
    expect(state.loading).toBe(false);
  });

  it('twoFactorRequired parks the flow without issuing a token', () => {
    const from = authReducer(initialAuthState, AuthActions.login({ request: { emailOrPhone: 'a', password: 'b' } }));

    const state = authReducer(from, AuthActions.twoFactorRequired());

    expect(state.awaitingTwoFactor).toBe(true);
    expect(state.token).toBeNull();
    expect(state.loading).toBe(false);
  });

  it('a successful 2FA login clears the awaiting flag', () => {
    const from: AuthState = { ...initialAuthState, awaitingTwoFactor: true };

    const state = authReducer(from, AuthActions.loginSuccess({ response }));

    expect(state.awaitingTwoFactor).toBe(false);
    expect(state.token).toBe('tok');
  });

  it('logout resets to the initial state', () => {
    const signedIn = authReducer(initialAuthState, AuthActions.loginSuccess({ response }));

    const state = authReducer(signedIn, AuthActions.logout());

    expect(state).toEqual(initialAuthState);
  });

  describe('loadUserFromStorage', () => {
    it('rehydrates token and user when both are present', () => {
      TokenStorage.save('stored-tok', user, true);

      const state = authReducer(initialAuthState, AuthActions.loadUserFromStorage());

      expect(state.token).toBe('stored-tok');
      expect(state.user).toEqual(user);
    });

    it('leaves state untouched when storage is empty', () => {
      const state = authReducer(initialAuthState, AuthActions.loadUserFromStorage());

      expect(state).toEqual(initialAuthState);
    });

    it('leaves state untouched when the stored user is malformed JSON', () => {
      localStorage.setItem('cinema_token', 'stored-tok');
      localStorage.setItem('cinema_user', 'not-json{');

      const state = authReducer(initialAuthState, AuthActions.loadUserFromStorage());

      expect(state).toEqual(initialAuthState);
    });
  });
});
