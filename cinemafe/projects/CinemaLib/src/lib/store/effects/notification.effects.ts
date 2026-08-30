import { Injectable, inject } from '@angular/core';
import { map, tap } from 'rxjs/operators';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SnackBarComponent } from '../../components/snackbar';
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
      tap((error: any) =>
        this._matSnackBar.openFromComponent(SnackBarComponent, {
          ...this._commonConfig,
          data: {
            message: error
              ? (error.isApiException ? (JSON.parse(error.response).Message || error.response) : error)
              : 'Đã xảy ra lỗi.',
            className: 'error',
          },
        })
      )
    ), { dispatch: false }
  );
}
