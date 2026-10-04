import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import { CinemaServiceAgent, UserRoles, selectCurrentUser, showException } from 'CinemaLib';

/** localStorage key holding the theater an Admin picked in the topbar. */
export const STAFF_THEATER_STORAGE_KEY = 'cinema_staff_theater';

/**
 * The theater every staff screen is scoped to. Theater staff and managers are pinned to their own
 * (`user.theaterId`); an Admin has no theater of their own, so they pick one in the topbar and the
 * choice is remembered across reloads.
 */
@Injectable({ providedIn: 'root' })
export class TheaterContextService {
  private readonly _store = inject(Store);
  private readonly _svc = inject(CinemaServiceAgent.HttpService);

  private readonly _user = this._store.selectSignal(selectCurrentUser);

  readonly isAdmin = computed(() => this._user()?.userTypeName === UserRoles.Admin);

  /** Theaters an Admin can choose from; stays empty for everyone else. */
  readonly theaters = signal<CinemaServiceAgent.TheaterDTO[]>([]);

  private readonly _picked = signal<string | null>(this._read());

  /** Theater id in force, or null while an Admin has not picked one (or a user has none). */
  readonly currentTheaterId = computed<string | null>(() =>
    this.isAdmin() ? this._picked() : (this._user()?.theaterId ?? null));

  readonly currentTheaterName = computed(() => {
    const id = this.currentTheaterId();
    return this.theaters().find(t => t.id === id)?.name ?? null;
  });

  private _loaded = false;

  constructor() {
    effect(() => {
      if (this.isAdmin() && !this._loaded) {
        this._loaded = true;
        this._loadTheaters();
      }
    });
  }

  /** Admin picks a theater (null clears the choice). */
  select(theaterId: string | null): void {
    this._picked.set(theaterId);
    try {
      if (theaterId) {
        localStorage.setItem(STAFF_THEATER_STORAGE_KEY, theaterId);
      } else {
        localStorage.removeItem(STAFF_THEATER_STORAGE_KEY);
      }
    } catch {
      /* storage unavailable — keep the in-memory choice only */
    }
  }

  private _loadTheaters(): void {
    this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200, filters: {} })).subscribe({
      next: result => {
        const theaters = result.results ?? [];
        this.theaters.set(theaters);
        // Forget a remembered theater that no longer exists.
        const picked = this._picked();
        if (picked && !theaters.some(t => t.id === picked)) {
          this.select(null);
        }
      },
      error: error => this._store.dispatch(showException({ error })),
    });
  }

  private _read(): string | null {
    try {
      return localStorage.getItem(STAFF_THEATER_STORAGE_KEY);
    } catch {
      return null;
    }
  }
}
