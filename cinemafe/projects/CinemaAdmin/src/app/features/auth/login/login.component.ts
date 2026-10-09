import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { LoginFormComponent, LoginStat, ShellBrand, UserRoles, logout } from 'CinemaLib';
import { environment } from '../../../../environments/environment';

/** Admin sign-in: the shared CinemaLib login layout with the admin copy, restricted to the Admin role. */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [LoginFormComponent],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly _store = inject(Store);
  private readonly _router = inject(Router);

  readonly brand: ShellBrand = { name: 'CINEMA', strong: 'ADMIN' };
  readonly allowedRoles: readonly string[] = [UserRoles.Admin];
  readonly staffAppUrl = environment.staffAppUrl;
  readonly stats: LoginStat[] = [
    { icon: 'movie', labelKey: 'login.statMovies' },
    { icon: 'theaters', labelKey: 'login.statTheaters' },
    { icon: 'confirmation_number', labelKey: 'login.statTickets' },
  ];
  /** Shown after a non-Admin account was signed in and bounced back here. */
  readonly rejectedNotice: string | null = inject(ActivatedRoute).snapshot.queryParamMap.has('rejected')
    ? 'login.notAdminAccount'
    : null;

  /**
   * A non-Admin account signed in. The auth effects are about to navigate to the return URL, so sign out
   * and come back here after that navigation settles, carrying a flag that makes the page explain why.
   */
  onRejected(): void {
    setTimeout(() => {
      this._store.dispatch(logout());
      this._router.navigate(['/auth/login'], { queryParams: { rejected: 1 } });
    });
  }
}
