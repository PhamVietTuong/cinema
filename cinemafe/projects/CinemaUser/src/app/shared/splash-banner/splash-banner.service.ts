import { Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { NavigationEnd, Router } from '@angular/router';
import { catchError, filter, of, switchMap, take } from 'rxjs';
import { CinemaServiceAgent } from 'CinemaLib';
import { SplashBannerDialogComponent } from './splash-banner.dialog';

const KEY_PREFIX = 'cinema.splashBanner.seen.';
/** The popup never auto-opens while the visitor is mid-booking or signing in. */
const SUPPRESSED_PREFIXES = ['/booking', '/auth'];

/** Shows the active SplashBanner (if any) as a popup once per browser session, the first time the
 * visitor lands on a page outside booking/auth. Wired into the root App component's ngOnInit. */
@Injectable({ providedIn: 'root' })
export class SplashBannerService {
  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _router: Router,
  ) {
  }

  init(): void {
    this._router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      filter(e => !SUPPRESSED_PREFIXES.some(prefix => e.urlAfterRedirects.startsWith(prefix))),
      take(1),
      switchMap(() => this._svc.getActiveSplashBanner().pipe(catchError(() => of([])))),
    ).subscribe(banners => {
      const banner = banners[0];
      if (banner && !this._wasSeen(banner.id!)) {
        this._markSeen(banner.id!);
        this._dialog.open(SplashBannerDialogComponent, {
          data: banner,
          panelClass: 'splash-banner-panel',
          autoFocus: false,
          maxWidth: '95vw',
        });
      }
    });
  }

  private _wasSeen(id: string): boolean {
    try {
      return sessionStorage.getItem(KEY_PREFIX + id) != null;
    } catch {
      return false;
    }
  }

  private _markSeen(id: string): void {
    try {
      sessionStorage.setItem(KEY_PREFIX + id, '1');
    } catch {
      // Storage unavailable (private browsing, blocked site data) — the banner just shows every time.
    }
  }
}
