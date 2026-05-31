import { HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { take, switchMap } from 'rxjs/operators';
import { selectToken } from '../store/auth/auth.selectors';

export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const store = inject(Store);
  return store.select(selectToken).pipe(
    take(1),
    switchMap(token => {
      if (token) {
        const cloned = req.clone({
          setHeaders: { Authorization: `Bearer ${token}` }
        });
        return next(cloned);
      }
      return next(req);
    })
  );
};
