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
  'movie-types': 'Quản Lý Thể Loại Phim',
  'age-restrictions': 'Quản Lý Giới Hạn Độ Tuổi',
  'seat-types': 'Quản Lý Loại Ghế',
  'ticket-types': 'Quản Lý Loại Vé',
  'discount-types': 'Quản Lý Loại Giảm Giá',
  memberships: 'Quản Lý Hạng Thành Viên',
  'user-types': 'Quản Lý Loại Người Dùng',
  holidays: 'Quản Lý Ngày Lễ',
  news: 'Quản Lý Tin Tức',
  rooms: 'Quản Lý Phòng Chiếu',
  discounts: 'Quản Lý Mã Giảm Giá',
  'food-and-drinks': 'Quản Lý Đồ Ăn & Thức Uống',
  invoices: 'Quản Lý Hóa Đơn',
  'movie-type-details': 'Gán Thể Loại Cho Phim',
  'seat-ticket-pricing': 'Bảng Giá Theo Loại Ghế & Vé',
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
