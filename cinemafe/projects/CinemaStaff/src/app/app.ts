import { Component, OnInit, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { STAFF_APP_ROLES, ShellBrand, ShellUserFallback, selectCurrentUser, loadUserFromStorage, logout } from 'CinemaLib';
import { STAFF_MENU } from './staff-menu';
import { TheaterContextService } from './core/theater-context.service';

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly _store = inject(Store);
  readonly theaterContext = inject(TheaterContextService);

  user$: Observable<any> = this._store.select(selectCurrentUser);
  /** Drives the staff chrome. Gated on holding a staff role, not merely being signed in, so a customer
   *  who lands on /forbidden isn't shown a sidebar full of pages they can't open. */
  isStaff$: Observable<boolean> = this.user$.pipe(map(user => STAFF_APP_ROLES.includes(user?.userTypeName ?? '')));

  readonly menu = STAFF_MENU;
  readonly brand: ShellBrand = { name: 'CINEMA', strong: 'STAFF', icon: 'badge' };
  readonly userFallback: ShellUserFallback = { nameKey: 'staff.userFallback', email: '', initial: 'NV' };

  ngOnInit(): void {
    this._store.dispatch(loadUserFromStorage());
  }

  doLogout(): void {
    this._store.dispatch(logout());
  }

  onTheaterPicked(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.theaterContext.select(value || null);
  }
}
