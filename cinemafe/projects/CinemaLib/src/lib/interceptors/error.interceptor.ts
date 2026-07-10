import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { logout } from '../store/auth/auth.actions';
import { ToastService } from '../services/toast.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const store = inject(Store);
  const toast = inject(ToastService);
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        store.dispatch(logout());
        router.navigate(['/auth/login']);
      } else if (error.status === 0) {
        toast.error('Không thể kết nối máy chủ. Vui lòng kiểm tra kết nối mạng.');
      } else if (error.status === 403) {
        toast.error('Bạn không có quyền thực hiện thao tác này.');
      } else if (error.status >= 500) {
        toast.error('Đã xảy ra lỗi máy chủ. Vui lòng thử lại sau.');
      }
      // 400/404 stay component-handled (inline validation messages).
      return throwError(() => error);
    })
  );
};
