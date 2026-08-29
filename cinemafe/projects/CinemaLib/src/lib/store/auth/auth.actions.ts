import { createAction, props } from '@ngrx/store';
import { AuthResponse, LoginRequest, RegisterRequest } from '../../models/auth.models';

export const login = createAction('[Auth] Login', props<{ request: LoginRequest; rememberMe?: boolean }>());
export const loginSuccess = createAction('[Auth] Login Success', props<{ response: AuthResponse; rememberMe?: boolean }>());
export const loginFailure = createAction('[Auth] Login Failure', props<{ error: string }>());

export const register = createAction('[Auth] Register', props<{ request: RegisterRequest }>());
export const registerSuccess = createAction('[Auth] Register Success', props<{ response: AuthResponse }>());
export const registerFailure = createAction('[Auth] Register Failure', props<{ error: string }>());

export const twoFactorRequired = createAction('[Auth] Two Factor Required');

/** The signed-in user's own details changed (profile edit) — refresh the cached copy. */
export const profileUpdated = createAction('[Auth] Profile Updated', props<{ user: unknown }>());

export const logout = createAction('[Auth] Logout');
export const loadUserFromStorage = createAction('[Auth] Load User From Storage');
