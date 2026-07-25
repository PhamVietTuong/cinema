import { Component, OnInit, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { filter, map, startWith } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { Router, NavigationEnd } from '@angular/router';
import { selectIsAuthenticated, selectCurrentUser, loadUserFromStorage, logout, ThemeService } from 'CinemaLib';

/** Route segment → i18n key. The key is resolved with the `translate` pipe in
 *  the template so the page title reacts to language switches too. */
const PAGE_TITLE_KEYS: Record<string, string> = {
  dashboard: 'pageTitle.dashboard',
  reports: 'pageTitle.reports',
  movies: 'pageTitle.movies',
  theaters: 'pageTitle.theaters',
  showtimes: 'pageTitle.showtimes',
  users: 'pageTitle.users',
  'movie-types': 'pageTitle.movieTypes',
  'age-restrictions': 'pageTitle.ageRestrictions',
  'discount-types': 'pageTitle.discountTypes',
  memberships: 'pageTitle.memberships',
  'user-types': 'pageTitle.userTypes',
  holidays: 'pageTitle.holidays',
  news: 'pageTitle.news',
  discounts: 'pageTitle.discounts',
  invoices: 'pageTitle.invoices',
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
  pageTitleKey$: Observable<string>;

  /** Mobile sidebar drawer open state (ignored on desktop where the rail is static). */
  menuOpen = false;

  /** Dark/light state — persisted and applied to <html data-theme> by the service. */
  readonly theme = inject(ThemeService);

  constructor(private _store: Store, private _router: Router) {
    this.isAuth$ = this._store.select(selectIsAuthenticated);
    this.user$ = this._store.select(selectCurrentUser);
    const nav$ = this._router.events.pipe(filter(e => e instanceof NavigationEnd));
    // Close the mobile drawer whenever navigation completes.
    nav$.subscribe(() => { this.menuOpen = false; });
    this.pageTitleKey$ = nav$.pipe(
      map(() => this._titleKeyFromUrl(this._router.url)),
      startWith(this._titleKeyFromUrl(this._router.url)),
    );
  }

  toggleMenu(): void { this.menuOpen = !this.menuOpen; }
  closeMenu(): void { this.menuOpen = false; }

  ngOnInit(): void {
    this._store.dispatch(loadUserFromStorage());
  }

  toggleTheme(): void {
    this.theme.toggle();
  }

  doLogout(): void {
    this._store.dispatch(logout());
  }

  private _titleKeyFromUrl(url: string): string {
    const seg = url.split('?')[0].split('/').filter(Boolean)[0] ?? 'dashboard';
    return PAGE_TITLE_KEYS[seg] ?? 'pageTitle.default';
  }
}
