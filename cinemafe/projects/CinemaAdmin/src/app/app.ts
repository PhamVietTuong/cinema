import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { filter, map, startWith } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { Router, NavigationEnd } from '@angular/router';
import { selectIsAuthenticated, selectCurrentUser, loadUserFromStorage, logout } from 'CinemaLib';

const PAGE_TITLES: Record<string, string> = {
  dashboard: 'Tổng Quan Hệ Thống',
  movies: 'Quản Lý Phim',
  theaters: 'Quản Lý Rạp Chiếu',
  showtimes: 'Quản Lý Lịch Chiếu',
  users: 'Quản Lý Người Dùng',
};

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  isAuth$: Observable<boolean>;
  user$: Observable<any>;
  pageTitle$: Observable<string>;

  constructor(private _store: Store, private _router: Router) {
    this.isAuth$ = this._store.select(selectIsAuthenticated);
    this.user$ = this._store.select(selectCurrentUser);
    this.pageTitle$ = this._router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(() => this._titleFromUrl(this._router.url)),
      startWith(this._titleFromUrl(this._router.url)),
    );
  }

  ngOnInit(): void {
    this._store.dispatch(loadUserFromStorage());
  }

  doLogout(): void {
    this._store.dispatch(logout());
  }

  private _titleFromUrl(url: string): string {
    const seg = url.split('?')[0].split('/').filter(Boolean)[0] ?? 'dashboard';
    return PAGE_TITLES[seg] ?? 'Bảng Điều Khiển';
  }
}
