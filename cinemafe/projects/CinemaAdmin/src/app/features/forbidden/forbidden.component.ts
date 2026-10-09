import { Component, computed, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { ForbiddenExtraLink, ForbiddenPageComponent, STAFF_APP_ROLES, selectCurrentUser } from 'CinemaLib';
import { environment } from '../../../environments/environment';

/**
 * Landing page for an authenticated user who is not an Admin.
 *
 * The admin app has no non-admin surface, so `adminGuard` needs a target that is
 * itself unguarded — redirecting to '/' would bounce back into /dashboard and loop.
 */
@Component({
  selector: 'app-forbidden',
  standalone: true,
  imports: [ForbiddenPageComponent],
  templateUrl: './forbidden.component.html',
})
export class ForbiddenComponent {
  private _store = inject(Store);

  readonly staffAppUrl = environment.staffAppUrl;
  private readonly _user = this._store.selectSignal(selectCurrentUser);
  /** Staff accounts (any staff-app role) get a link to the Staff app instead of a dead end. */
  readonly isStaff = computed(() => STAFF_APP_ROLES.includes(this._user()?.userTypeName ?? ''));

  readonly staffLink = computed<ForbiddenExtraLink | null>(() =>
    this.isStaff() ? { href: this.staffAppUrl, labelKey: 'forbidden.openStaffApp', icon: 'open_in_new' } : null);
}
