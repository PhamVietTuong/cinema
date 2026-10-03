import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { map, take } from 'rxjs/operators';
import { selectCurrentUser } from '../store/auth/auth.selectors';
import { BACK_OFFICE_ROLES, UserRoles } from '../models/roles.models';

/**
 * Allows users whose role is in `roles`, otherwise sends them to /forbidden
 * (which must stay outside any guarded route tree to avoid redirect loops).
 */
export function roleGuard(roles: readonly string[]): CanActivateFn {
  return () => {
    const store = inject(Store);
    const router = inject(Router);
    return store.select(selectCurrentUser).pipe(
      take(1),
      map(user => roles.includes(user?.userTypeName ?? '') ? true : router.createUrlTree(['/forbidden']))
    );
  };
}

/**
 * Landing redirect for the admin app root: Admin -> /dashboard, other back-office
 * roles -> /inventory, everyone else -> /forbidden.
 */
export const homeRedirectGuard: CanActivateFn = () => {
  const store = inject(Store);
  const router = inject(Router);
  return store.select(selectCurrentUser).pipe(
    take(1),
    map(user => {
      const role = user?.userTypeName ?? '';
      if (role === UserRoles.Admin) {
        return router.createUrlTree(['/dashboard']);
      }
      if (BACK_OFFICE_ROLES.includes(role)) {
        return router.createUrlTree(['/inventory']);
      }
      return router.createUrlTree(['/forbidden']);
    })
  );
};
