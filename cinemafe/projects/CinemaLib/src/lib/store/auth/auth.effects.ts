import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, switchMap, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { IdentityServiceAgent } from '../../services/identity-http.service';
import { TokenStorage } from './token-storage';
import * as AuthActions from './auth.actions';

@Injectable()
export class AuthEffects {
  private actions$ = inject(Actions);
  private _identityService = inject(IdentityServiceAgent.HttpService);
  private _router = inject(Router);

  login$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AuthActions.login),
      switchMap(({ request, rememberMe }) =>
        this._identityService.login(IdentityServiceAgent.LoginRequest.fromJS(request)).pipe(
          map(response => AuthActions.loginSuccess({ response: response as any, rememberMe })),
          catchError(err => of(AuthActions.loginFailure({ error: err.error?.error ?? 'Login failed' })))
        )
      )
    )
  );

  register$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AuthActions.register),
      switchMap(({ request }) =>
        this._identityService.register(IdentityServiceAgent.RegisterRequest.fromJS(request)).pipe(
          map(response => AuthActions.registerSuccess({ response: response as any })),
          catchError(err => of(AuthActions.registerFailure({ error: err.error?.error ?? 'Registration failed' })))
        )
      )
    )
  );

  loginSuccess$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AuthActions.loginSuccess, AuthActions.registerSuccess),
      tap(action => {
        const response = (action as any).response;
        // Register (and a login without an explicit choice) defaults to persisting.
        const remember = 'rememberMe' in action ? (action as any).rememberMe !== false : true;
        TokenStorage.save(response.token, response.user, remember);
        this._router.navigateByUrl(this._resolveReturnUrl());
      })
    ), { dispatch: false }
  );

  logout$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AuthActions.logout),
      tap(() => {
        TokenStorage.clear();
        this._router.navigate(['/auth/login']);
      })
    ), { dispatch: false }
  );

  // Read ?returnUrl= off the current URL (set by authGuard). Only accept an
  // internal path to avoid open-redirects; default to home.
  private _resolveReturnUrl(): string {
    const target = this._router.parseUrl(this._router.url).queryParams['returnUrl'];
    if (target && target.startsWith('/') && !target.startsWith('//')) {
      return target;
    }
    return '/';
  }
}
