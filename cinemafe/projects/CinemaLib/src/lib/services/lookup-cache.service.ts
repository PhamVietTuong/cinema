import { Injectable, inject } from '@angular/core';
import { Observable, catchError, shareReplay, throwError } from 'rxjs';
import { CinemaServiceAgent } from './cinema-http.service';

const LOOKUP_PAGE_SIZE = 200;
const DEFAULT_TTL_MS = 5 * 60 * 1000;

type Entry = { expires: number; stream$: Observable<unknown> };

/**
 * Short-lived, in-memory cache of the small reference lists (theaters, memberships, movie types, age
 * restrictions, a theater's rooms) that many screens and dialogs load just to fill a dropdown. Each list is
 * fetched once per TTL and replayed to every subscriber; a failed fetch is evicted so the next caller retries.
 * Call `invalidate()` after creating, editing or deleting one of these entities so dropdowns see the change.
 */
@Injectable({ providedIn: 'root' })
export class LookupCacheService {
  private readonly _svc = inject(CinemaServiceAgent.HttpService);
  private readonly _entries = new Map<string, Entry>();

  theaters(): Observable<CinemaServiceAgent.DefaultSearchResultsOfTheaterDTO> {
    return this._get('theaters', () => this._svc.getTheaters(this._page()));
  }

  memberships(): Observable<CinemaServiceAgent.DefaultSearchResultsOfMemberShipDTO> {
    return this._get('memberships', () => this._svc.getMemberShips(this._page()));
  }

  movieTypes(): Observable<CinemaServiceAgent.DefaultSearchResultsOfMovieTypeDTO> {
    return this._get('movieTypes', () => this._svc.getMovieTypes(this._page()));
  }

  ageRestrictions(): Observable<CinemaServiceAgent.DefaultSearchResultsOfAgeRestrictionDTO> {
    return this._get('ageRestrictions', () => this._svc.getAgeRestrictions(this._page()));
  }

  /** The rooms of one theater (the `theaterId` filter the rooms list endpoint understands). */
  rooms(theaterId: string): Observable<CinemaServiceAgent.DefaultSearchResultsOfRoomDTO> {
    return this._get(`rooms:${theaterId}`, () => this._svc.getRooms(this._page({ theaterId })));
  }

  /** Drops cached lists: those whose key starts with `prefix` (e.g. `'rooms'`, `'theaters'`), or all of them. */
  invalidate(prefix?: string): void {
    for (const key of [...this._entries.keys()]) {
      if (!prefix || key.startsWith(prefix)) {
        this._entries.delete(key);
      }
    }
  }

  private _page(filters?: Record<string, unknown>): CinemaServiceAgent.PagingSearchDTO {
    return CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: LOOKUP_PAGE_SIZE, filters: filters ?? {} });
  }

  private _get<T>(key: string, load: () => Observable<T>, ttlMs = DEFAULT_TTL_MS): Observable<T> {
    const hit = this._entries.get(key);
    if (hit && hit.expires > Date.now()) {
      return hit.stream$ as Observable<T>;
    }
    const stream$ = load().pipe(
      shareReplay({ bufferSize: 1, refCount: false }),
      catchError(err => {
        if (this._entries.get(key)?.stream$ === stream$) {
          this._entries.delete(key);
        }
        return throwError(() => err);
      }),
    );
    this._entries.set(key, { expires: Date.now() + ttlMs, stream$ });
    return stream$;
  }
}
