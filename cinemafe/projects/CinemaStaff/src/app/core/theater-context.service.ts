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

  /** Identity the theater list was loaded for, so a different user in the same session reloads it. */
  private _loadedFor: string | null = null;

  constructor() {
    effect(() => {
      const user = this._user();
      const key = user ? `${this.isAdmin() ? 'admin' : 'pinned'}:${user.id}:${user.theaterId ?? ''}` : null;
      if (key === this._loadedFor) {
        return;
      }
      this._loadedFor = key;
      this.theaters.set([]);
      if (!user) {
        return;
      }
      if (this.isAdmin()) {
        this._loadTheaters();
      } else if (user.theaterId) {
        // A pinned user has no picker, but still needs their own theater name for headings and the home greeting.
        this._loadOwnTheater(user.theaterId);
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

  private _loadOwnTheater(id: string): void {
    this._svc.getTheater(id).subscribe({
      next: theater => this.theaters.set([theater]),
      error: error => this._store.dispatch(showException({ error })),
    });
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
