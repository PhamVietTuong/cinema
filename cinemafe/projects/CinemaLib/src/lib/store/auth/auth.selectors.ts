import { createFeatureSelector, createSelector } from '@ngrx/store';
import { AuthState } from './auth.state';

export const selectAuthState = createFeatureSelector<AuthState>('auth');
export const selectCurrentUser = createSelector(selectAuthState, s => s.user);
export const selectToken = createSelector(selectAuthState, s => s.token);
export const selectIsAuthenticated = createSelector(selectToken, token => !!token);
export const selectAuthLoading = createSelector(selectAuthState, s => s.loading);
export const selectAuthError = createSelector(selectAuthState, s => s.error);
export const selectAwaitingTwoFactor = createSelector(selectAuthState, s => s.awaitingTwoFactor);
export const selectIsAdmin = createSelector(selectCurrentUser, user => user?.userTypeName === 'Admin');
