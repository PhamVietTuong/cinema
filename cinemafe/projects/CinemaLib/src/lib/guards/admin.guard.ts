import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { map, take } from 'rxjs/operators';
import { selectCurrentUser } from '../store/auth/auth.selectors';

/**
 * Allows Admins through, otherwise sends the user to /forbidden.
 *
 * The redirect target must be a route that is NOT itself behind this guard or an
 * authGuard tree whose root redirects back into one. Redirecting to '/' loops
 * forever in the admin app ('/' -> /dashboard -> denied -> '/' -> ...) and hangs
 * the browser, so /forbidden is deliberately kept unguarded.
 */
export const adminGuard: CanActivateFn = () => {
  const store = inject(Store);
  const router = inject(Router);
  return store.select(selectCurrentUser).pipe(
    take(1),
    map(user => (user?.userTypeName === 'Admin') ? true : router.createUrlTree(['/forbidden']))
  );
};
