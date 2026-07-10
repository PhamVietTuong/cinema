import { createReducer, on } from '@ngrx/store';
import { initialAuthState } from './auth.state';
import { TokenStorage } from './token-storage';
import * as AuthActions from './auth.actions';

export const authReducer = createReducer(
  initialAuthState,
  on(AuthActions.login, AuthActions.register, state => ({ ...state, loading: true, error: null })),
  on(AuthActions.loginSuccess, AuthActions.registerSuccess, (state, { response }) => ({
    ...state, loading: false, token: response.token, user: response.user, error: null
  })),
  on(AuthActions.loginFailure, AuthActions.registerFailure, (state, { error }) => ({
    ...state, loading: false, error
  })),
  on(AuthActions.logout, () => ({ ...initialAuthState })),
  on(AuthActions.loadUserFromStorage, state => {
    const token = TokenStorage.getToken();
    const userStr = TokenStorage.getUser();
    if (token && userStr) {
      try {
        return { ...state, token, user: JSON.parse(userStr) };
      } catch { return state; }
    }
    return state;
  })
);
