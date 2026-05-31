import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, switchMap, tap } from 'rxjs/operators';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { IdentityServiceAgent } from '../../services/identity-http.service';
import * as AuthActions from './auth.actions';

@Injectable()
export class AuthEffects {
  private actions$ = inject(Actions);
  private _identityService = inject(IdentityServiceAgent.HttpService);
  private _router = inject(Router);

  login$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AuthActions.login),
      switchMap(({ request }) =>
        this._identityService.login(IdentityServiceAgent.LoginRequest.fromJS(request)).pipe(
          map(response => AuthActions.loginSuccess({ response: response as any })),
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
      tap(({ response }) => {
        localStorage.setItem('cinema_token', (response as any).token);
        localStorage.setItem('cinema_user', JSON.stringify((response as any).user));
        this._router.navigate(['/']);
      })
    ), { dispatch: false }
  );

  logout$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AuthActions.logout),
      tap(() => {
        localStorage.removeItem('cinema_token');
        localStorage.removeItem('cinema_user');
        this._router.navigate(['/auth/login']);
      })
    ), { dispatch: false }
  );
}
