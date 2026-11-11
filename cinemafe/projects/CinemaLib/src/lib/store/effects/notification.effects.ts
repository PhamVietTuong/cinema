import { Injectable, inject } from '@angular/core';
import { filter, map, tap } from 'rxjs/operators';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SnackBarComponent } from '../../components/snackbar';
import { rawApiErrorMessage, translateApiError } from '../../services/api-error';
import * as NotificationActions from '../actions/notification.actions';

@Injectable()
export class NotificationEffects {
  private _actions$ = inject(Actions);
  private _matSnackBar = inject(MatSnackBar);

  private _commonConfig = {
    duration: 3000,
    verticalPosition: 'top' as const,
  };

  showSuccess$ = createEffect(() =>
    this._actions$.pipe(
      ofType(NotificationActions.showSuccess),
      map(action => action.message),
      tap(message =>
        this._matSnackBar.openFromComponent(SnackBarComponent, {
          ...this._commonConfig,
          data: { message: message || 'Thành công.', className: 'success' },
        })
      )
    ), { dispatch: false }
  );

  showError$ = createEffect(() =>
    this._actions$.pipe(
      ofType(NotificationActions.showError),
      map(action => action.message),
      tap(message =>
        this._matSnackBar.openFromComponent(SnackBarComponent, {
          ...this._commonConfig,
          data: { message: message || 'Đã xảy ra lỗi.', className: 'error' },
        })
      )
    ), { dispatch: false }
  );

  showException$ = createEffect(() =>
    this._actions$.pipe(
      ofType(NotificationActions.showException),
      map(action => action.error),
      // The error interceptor already toasts (or redirects for) these: a bare 401 means the session ended and it
      // signed the user out; a bare 403 (no message body) already got the "no permission" toast.
      // A second generic "unexpected server error" toast on top of them is noise (and would replace the specific one).
      filter((error: any) => !((error?.status === 401 || error?.status === 403) && rawApiErrorMessage(error) === null)),
      tap((error: any) =>
        this._matSnackBar.openFromComponent(SnackBarComponent, {
          ...this._commonConfig,
          data: {
            message: this._errorMessage(error),
            className: 'error',
          },
        })
      )
    ), { dispatch: false }
  );

  /** Pulls a readable message out of an NSwag ApiException body ({ error | Message | message }), never raw JSON. */
  private _errorMessage(error: any): string {
    const fallback = 'Đã xảy ra lỗi.';
    if (!error) {
      return fallback;
    }
    if (error.isApiException) {
      try {
        const body = JSON.parse(error.response);
        return translateApiError(body.Message || body.message || body.error || error.response || fallback);
      } catch {
        return translateApiError(error.response || error.message || fallback);
      }
    }
    return translateApiError(typeof error === 'string' ? error : (error.message || fallback));
  }
}
