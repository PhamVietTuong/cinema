import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { LoginFormComponent, LoginStat, STAFF_APP_ROLES, ShellBrand, logout } from 'CinemaLib';

/** Staff sign-in: the shared CinemaLib login layout, restricted to staff roles. */
@Component({
  selector: 'staff-login',
  standalone: true,
  imports: [LoginFormComponent],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly _store = inject(Store);
  private readonly _router = inject(Router);

  readonly brand: ShellBrand = { name: 'CINEMA', strong: 'STAFF', icon: 'badge' };
  readonly allowedRoles = STAFF_APP_ROLES;
  readonly stats: LoginStat[] = [
    { icon: 'point_of_sale', labelKey: 'login.statPos' },
    { icon: 'qr_code_scanner', labelKey: 'login.statGate' },
    { icon: 'inventory_2', labelKey: 'login.statWarehouse' },
  ];
  /** Shown after a non-staff account was signed in and bounced back here. */
  private readonly _queryParams = toSignal(inject(ActivatedRoute).queryParamMap);
  readonly rejectedNotice = computed(() => {
    if (this._queryParams()?.has('rejected')) {
      return 'login.notStaffAccount';
    }
    return null;
  });

  /**
   * A non-staff account signed in. The auth effects are about to navigate to the return URL, so sign out
   * and come back here after that navigation settles, carrying a flag that makes the page explain why.
   */
  onRejected(): void {
    setTimeout(() => {
      this._store.dispatch(logout());
      this._router.navigate(['/auth/login'], { queryParams: { rejected: 1 } });
    });
  }
}
