import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { BreakpointObserver } from '@angular/cdk/layout';
import { Observable } from 'rxjs';
import { filter, map, startWith } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { Router, NavigationEnd } from '@angular/router';
import { selectCurrentUser, selectIsAdmin, selectIsBackOffice, loadUserFromStorage, logout, ThemeService } from 'CinemaLib';

/** Sidebar becomes an off-canvas mat-sidenav drawer below this width; matches app.scss's own breakpoint. */
const MOBILE_QUERY = '(max-width: 768px)';

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
  inventory: 'warehouse.pageTitle.inventory',
  'storage-plans': 'warehouse.pageTitle.storagePlans',
};

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  /** Drives the admin chrome. Gated on being an Admin, not merely signed in, so a customer
   *  who lands on /forbidden isn't shown a sidebar full of pages they can't open. */
  isBackOffice$: Observable<boolean>;
  /** Admin-only nav links. */
  isAdmin$: Observable<boolean>;
  user$: Observable<any>;
  pageTitleKey$: Observable<string>;

  /** Mobile sidebar drawer open state (ignored on desktop where the rail is static). */
  menuOpen = false;

  private readonly _breakpoints = inject(BreakpointObserver);
  private readonly _cd = inject(ChangeDetectorRef);

  /** Below the mobile breakpoint the sidenav becomes an off-canvas 'over' drawer; above it, a static 'side' rail. */
  isMobile = this._breakpoints.isMatched(MOBILE_QUERY);

  /** Dark/light state — persisted and applied to <html data-theme> by the service. */
  readonly theme = inject(ThemeService);

  constructor(private _store: Store, private _router: Router) {
    this.isAdmin$ = this._store.select(selectIsAdmin);
    this.isBackOffice$ = this._store.select(selectIsBackOffice);
    this.user$ = this._store.select(selectCurrentUser);
    const nav$ = this._router.events.pipe(filter(e => e instanceof NavigationEnd));
    // Close the mobile drawer whenever navigation completes.
    nav$.subscribe(() => { this.menuOpen = false; });
    this.pageTitleKey$ = nav$.pipe(
      map(() => this._titleKeyFromUrl(this._router.url)),
      startWith(this._titleKeyFromUrl(this._router.url)),
    );
    this._breakpoints.observe(MOBILE_QUERY).subscribe(result => {
      this.isMobile = result.matches;
      this._cd.markForCheck();
    });
  }

  toggleMenu(): void { this.menuOpen = !this.menuOpen; }

  /** mat-sidenav emits this on any open/close (backdrop click, escape key, or our own toggle). */
  onSidenavOpenedChange(opened: boolean): void {
    if (this.isMobile) {
      this.menuOpen = opened;
    }
  }

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
