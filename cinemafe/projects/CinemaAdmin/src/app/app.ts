import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { filter, map, startWith } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { Router, NavigationEnd } from '@angular/router';
import { selectIsAuthenticated, selectCurrentUser, loadUserFromStorage, logout } from 'CinemaLib';

const PAGE_TITLES: Record<string, string> = {
  dashboard: 'Tổng Quan Hệ Thống',
  reports: 'Báo Cáo Doanh Thu',
  movies: 'Quản Lý Phim',
  theaters: 'Quản Lý Rạp Chiếu',
  showtimes: 'Quản Lý Lịch Chiếu',
  users: 'Quản Lý Người Dùng',
  'movie-types': 'Quản Lý Thể Loại Phim',
  'age-restrictions': 'Quản Lý Giới Hạn Độ Tuổi',
  'discount-types': 'Quản Lý Loại Giảm Giá',
  memberships: 'Quản Lý Hạng Thành Viên',
  'user-types': 'Quản Lý Loại Người Dùng',
  holidays: 'Quản Lý Ngày Lễ',
  news: 'Quản Lý Tin Tức',
  discounts: 'Quản Lý Mã Giảm Giá',
  invoices: 'Quản Lý Hóa Đơn',
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

  /** Mobile sidebar drawer open state (ignored on desktop where the rail is static). */
  menuOpen = false;

  /** Dark-theme state, persisted to localStorage and applied to <html data-theme>. */
  isDark = false;

  constructor(private _store: Store, private _router: Router) {
    this.isAuth$ = this._store.select(selectIsAuthenticated);
    this.user$ = this._store.select(selectCurrentUser);
    const nav$ = this._router.events.pipe(filter(e => e instanceof NavigationEnd));
    // Close the mobile drawer whenever navigation completes.
    nav$.subscribe(() => { this.menuOpen = false; });
    this.pageTitle$ = nav$.pipe(
      map(() => this._titleFromUrl(this._router.url)),
      startWith(this._titleFromUrl(this._router.url)),
    );
  }

  toggleMenu(): void { this.menuOpen = !this.menuOpen; }
  closeMenu(): void { this.menuOpen = false; }

  ngOnInit(): void {
    this._store.dispatch(loadUserFromStorage());
    this.isDark = localStorage.getItem('ad_theme') === 'dark';
    this._applyTheme();
  }

  toggleTheme(): void {
    this.isDark = !this.isDark;
    localStorage.setItem('ad_theme', this.isDark ? 'dark' : 'light');
    this._applyTheme();
  }

  private _applyTheme(): void {
    document.documentElement.setAttribute('data-theme', this.isDark ? 'dark' : 'light');
  }

  doLogout(): void {
    this._store.dispatch(logout());
  }

  private _titleFromUrl(url: string): string {
    const seg = url.split('?')[0].split('/').filter(Boolean)[0] ?? 'dashboard';
    return PAGE_TITLES[seg] ?? 'Bảng Điều Khiển';
  }
}
