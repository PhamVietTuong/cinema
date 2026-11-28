import { Injectable, computed, signal } from '@angular/core';

const STORAGE_KEY = 'cu.watchlist';

/**
 * "Xem sau" (watch later) list. Purely client-side: movie ids kept in localStorage — no backend
 * involved. Every storage access is guarded since it can throw (private mode, blocked site data).
 */
@Injectable({ providedIn: 'root' })
export class WatchlistService {
  private readonly _ids = signal<readonly string[]>(this._read());

  /** Saved movie ids, newest first. */
  readonly ids = this._ids.asReadonly();
  readonly count = computed(() => this._ids().length);

  has(id: string | undefined | null): boolean {
    return !!id && this._ids().includes(id);
  }

  /** Adds or removes the movie; returns true when it is now saved. */
  toggle(id: string): boolean {
    const current = this._ids();
    const saved = !current.includes(id);
    const next = saved ? [id, ...current] : current.filter(x => x !== id);
    this._ids.set(next);
    this._write(next);
    return saved;
  }

  private _read(): string[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      const parsed: unknown = raw ? JSON.parse(raw) : [];
      return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string') : [];
    } catch {
      return [];
    }
  }

  private _write(ids: readonly string[]): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(ids));
    } catch {
      // Storage unavailable: the list still works for this session.
    }
  }
}
