import { Component, OnInit, Type, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { filter } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { Router, NavigationEnd } from '@angular/router';
import { selectIsAuthenticated, selectCurrentUser, loadUserFromStorage, logout, ThemeService } from 'CinemaLib';
import { SplashBannerService } from './shared/splash-banner/splash-banner.service';

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  isAuth$: Observable<boolean>;
  user$: Observable<any>;

  /** The assistant widget, loaded in an idle slot after startup so it stays out of the initial bundle
   * (an `@defer` block can't split it: this root component lives in an NgModule). */
  readonly chatbot = signal<Type<unknown> | null>(null);

  /** Mobile nav menu open state. */
  menuOpen = false;

  /** Dark/light state — persisted and applied to <html data-theme> by the service. */
  readonly theme = inject(ThemeService);

  constructor(private _store: Store, private _router: Router, private _splashBanner: SplashBannerService) {
    this.isAuth$ = this._store.select(selectIsAuthenticated);
    this.user$ = this._store.select(selectCurrentUser);
    // Close the mobile menu after navigating.
    this._router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => { this.menuOpen = false; });
  }

  ngOnInit(): void {
    this._store.dispatch(loadUserFromStorage());
    this._splashBanner.init();
    this._loadChatbotWhenIdle();
  }

  private _loadChatbotWhenIdle(): void {
    const load = () => import('./shared/chatbot/chatbot.component').then(m => this.chatbot.set(m.ChatbotComponent));
    const idle = (window as { requestIdleCallback?: (cb: () => void) => number }).requestIdleCallback;
    if (idle) {
      idle.call(window, () => { void load(); });
    } else {
      setTimeout(() => { void load(); }, 1500);
    }
  }

  toggleMenu(): void { this.menuOpen = !this.menuOpen; }
  closeMenu(): void { this.menuOpen = false; }

  /** mat-sidenav emits this on any open/close (backdrop click, escape key, or our own toggle). */
  onSidenavOpenedChange(opened: boolean): void { this.menuOpen = opened; }

  toggleTheme(): void { this.theme.toggle(); }

  doLogout(): void {
    this.menuOpen = false;
    this._store.dispatch(logout());
  }
}
