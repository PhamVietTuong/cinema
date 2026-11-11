import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { Injector, inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { logout } from '../store/auth/auth.actions';
import { ToastService } from '../services/toast.service';
import { matchApiError } from '../services/api-error-catalog';
import { rawApiErrorMessage, translateApiError } from '../services/api-error';

/**
 * 401 from these Identity calls means "wrong credentials / bad code", not "your session expired":
 * they are either anonymous or re-check a password for a signed-in user, so they must not sign the
 * user out (nor redirect, which would drop the login page's returnUrl).
 */
const CREDENTIAL_CHECK_URL = /\/api\/Identity\/(Login|Register|GoogleLogin|FacebookLogin|VerifyTwoFactor|ForgotPassword|ResetPassword|ConfirmEmail|ResendVerification|ChangePassword)(\?|$)/i;

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
      if (error.status === 401 && !CREDENTIAL_CHECK_URL.test(req.url)) {
        store.dispatch(logout());
        router.navigate(['/auth/login']);
      } else if (error.status === 0) {
        toast.error(t('errors.network'));
      } else if (error.status === 403) {
        // A 403 can carry a specific reason (e.g. a refused manager PIN); show it translated when known.
        const raw = rawApiErrorMessage(error);
        toast.error(raw && matchApiError(raw) ? translateApiError(raw) : t('errors.forbidden'));
      } else if (error.status >= 500) {
        toast.error(t('errors.server'));
      }
      // 400/404 stay component-handled (inline validation messages).
      return throwError(() => error);
    })
  );
};
