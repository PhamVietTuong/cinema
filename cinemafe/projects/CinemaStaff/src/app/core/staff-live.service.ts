import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import { CONCESSION_ROLES, StaffHubService, CinemaServiceAgent, selectCurrentUser } from 'CinemaLib';
import { TheaterContextService } from './theater-context.service';

/** Low-stock item count shown as the Kitchen menu badge. The menu is a module-level constant, so the count lives in a module-level signal. */
export const LOW_STOCK_BADGE = signal(0);

/**
 * App-wide live state for concession roles: keeps the staff hub connected (an Admin follows the theater
 * picked in the topbar) and tracks the theater's low-stock items for the Kitchen menu badge and panel.
 * It is instantiated once by the app shell, so the badge works on every page.
 */
@Injectable({ providedIn: 'root' })
export class StaffLiveService {
  private readonly _store = inject(Store);
  private readonly _hub = inject(StaffHubService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _concession = inject(CinemaServiceAgent.HttpService);

  private readonly _user = this._store.selectSignal(selectCurrentUser);

  private readonly _lowStock = signal<CinemaServiceAgent.LowStockItemDTO[]>([]);
  readonly lowStock = this._lowStock.asReadonly();
  /** Number of low-stock items; drives the Kitchen menu badge. */
  readonly lowStockCount = computed(() => this._lowStock().length);

  constructor() {
    effect(() => LOW_STOCK_BADGE.set(this.lowStockCount()));

    const isConcession =computed(() => CONCESSION_ROLES.includes(this._user()?.userTypeName ?? ''));

    effect(() => {
      if (!isConcession()) {
        return;
      }
      const theaterId = this._theater.currentTheaterId();
      const isAdmin = this._theater.isAdmin();
      void this._connect(isAdmin ? theaterId : null);
      if (theaterId) {
        this.refreshLowStock(theaterId);
      } else {
        this._lowStock.set([]);
      }
    });

    this._hub.stockLow$.subscribe(() => this.refreshLowStock());
    this._hub.reconnected$.subscribe(() => this.refreshLowStock());
  }

  /** Reloads the low-stock list for the current (or given) theater. */
  refreshLowStock(theaterId: string | null = this._theater.currentTheaterId()): void {
    if (!theaterId) {
      return;
    }
    this._concession.getLowStock(CinemaServiceAgent.GetLowStockRequest.fromJS({ theaterId })).subscribe({
      next: items => this._lowStock.set(items ?? []),
      // The badge is a convenience; a failed refresh keeps the previous list and the board still works.
      error: () => undefined,
    });
  }

  private async _connect(followTheaterId: string | null): Promise<void> {
    try {
      await this._hub.start();
      await this._hub.setTheater(followTheaterId);
    } catch {
      // Live updates are best effort; the kitchen board falls back to its manual refresh.
    }
  }
}
