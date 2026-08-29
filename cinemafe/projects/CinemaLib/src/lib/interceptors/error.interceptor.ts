import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { Injector, inject } from '@angular/core';
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
  // Resolved lazily, NOT with inject(TranslateService) here: the translation loader fetches its
  // JSON over the same HttpClient this interceptor is registered on, so eagerly injecting the
  // service while it is still being constructed is circular and silently breaks translation
  // loading app-wide. By the time a request actually fails, the service is safe to resolve.
  const injector = inject(Injector);
  const t = (key: string): string => {
    try { return injector.get(TranslateService).instant(key); } catch { return key; }
  };
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        store.dispatch(logout());
        router.navigate(['/auth/login']);
      } else if (error.status === 0) {
        toast.error(t('errors.network'));
      } else if (error.status === 403) {
        toast.error(t('errors.forbidden'));
      } else if (error.status >= 500) {
        toast.error(t('errors.server'));
      }
      // 400/404 stay component-handled (inline validation messages).
      return throwError(() => error);
    })
  );
};
