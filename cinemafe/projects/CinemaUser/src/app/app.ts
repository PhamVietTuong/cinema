import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { Store } from '@ngrx/store';
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

  constructor(private _store: Store) {
    this.isAuth$ = this._store.select(selectIsAuthenticated);
    this.user$ = this._store.select(selectCurrentUser);
  }

  ngOnInit(): void {
    this._store.dispatch(loadUserFromStorage());
  }

  doLogout(): void {
    this._store.dispatch(logout());
  }
}
