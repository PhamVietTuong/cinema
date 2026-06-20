import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { filter } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { Router, NavigationEnd } from '@angular/router';
import { selectIsAuthenticated, selectCurrentUser, loadUserFromStorage, logout } from 'CinemaLib';

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  isAuth$: Observable<boolean>;
  user$: Observable<any>;

  /** Mobile nav menu open state. */
  menuOpen = false;

  constructor(private _store: Store, private _router: Router) {
    this.isAuth$ = this._store.select(selectIsAuthenticated);
    this.user$ = this._store.select(selectCurrentUser);
    // Close the mobile menu after navigating.
    this._router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => { this.menuOpen = false; });
  }

  ngOnInit(): void {
    this._store.dispatch(loadUserFromStorage());
  }

  toggleMenu(): void { this.menuOpen = !this.menuOpen; }
  closeMenu(): void { this.menuOpen = false; }

  doLogout(): void {
    this.menuOpen = false;
    this._store.dispatch(logout());
  }
}
