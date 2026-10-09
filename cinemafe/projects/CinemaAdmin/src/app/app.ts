import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { Store } from '@ngrx/store';
import { ShellBrand, ShellUserFallback, selectCurrentUser, selectIsAdmin,loadUserFromStorage, logout } from 'CinemaLib';
import { ADMIN_MENU } from './admin-menu';

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  /** Drives the admin chrome. Gated on being an Admin, not merely signed in, so a staff member
   *  or customer who lands on /forbidden isn't shown a sidebar full of pages they can't open. */
  isAdmin$: Observable<boolean>;
  user$: Observable<any>;

  readonly menu = ADMIN_MENU;
  readonly brand: ShellBrand = { name: 'CINEMA', strong: 'ADMIN' };
  readonly userFallback: ShellUserFallback = { nameKey: 'profile.adminFallback', email: 'admin@cinema.vn', initial: 'QT' };

  constructor(private _store: Store) {
    this.isAdmin$ = this._store.select(selectIsAdmin);
    this.user$ = this._store.select(selectCurrentUser);
  }

  ngOnInit(): void {
    this._store.dispatch(loadUserFromStorage());
  }

  doLogout(): void {
    this._store.dispatch(logout());
  }
}
