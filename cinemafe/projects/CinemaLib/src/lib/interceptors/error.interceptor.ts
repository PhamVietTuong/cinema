import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { logout } from '../store/auth/auth.actions';
import { ToastService } from '../services/toast.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const store = inject(Store);
  const toast = inject(ToastService);
  // These were hardcoded Vietnamese, so an English session got Vietnamese error toasts.
  const translate = inject(TranslateService);
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        store.dispatch(logout());
        router.navigate(['/auth/login']);
      } else if (error.status === 0) {
        toast.error(translate.instant('errors.network'));
      } else if (error.status === 403) {
        toast.error(translate.instant('errors.forbidden'));
      } else if (error.status >= 500) {
        toast.error(translate.instant('errors.server'));
      }
      // 400/404 stay component-handled (inline validation messages).
      return throwError(() => error);
    })
  );
};
