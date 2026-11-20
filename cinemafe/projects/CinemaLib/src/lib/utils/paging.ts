import { Observable, from, of } from 'rxjs';
import { map, mergeMap, switchMap, toArray } from 'rxjs/operators';

/** The subset of a paged search result that `fetchAllPages` needs to decide whether to keep going. */
export interface FetchablePage<T> {
  results?: T[];
  totalCount?: number;
}

/** How many page requests `fetchAllPages` keeps in flight at once after the first page. */
export const DEFAULT_PAGE_CONCURRENCY = 6;

/**
 * Collects every row of a paged search and emits the combined array once.
 *
 * Page 1 (1-based `fetchPage` index) is fetched first because its `totalCount` says how many more
 * pages exist; the remaining pages are then requested in parallel, at most `concurrency` at a time,
 * instead of one after another. Rows come back in page order. Nothing more is fetched when page 1
 * is empty, shorter than `pageSize` (the last page), or has no `totalCount` (unknown total).
 * Rows are de-duplicated by `getId` as a safety net against an unstable sort on the server.
 */
export function fetchAllPages<T>(
  fetchPage: (pageIndex: number) => Observable<FetchablePage<T>>,
  pageSize: number,
  getId: (item: T) => unknown,
  concurrency: number = DEFAULT_PAGE_CONCURRENCY,
): Observable<T[]> {
  return fetchPage(1).pipe(
    switchMap(first => {
      const firstItems = first.results ?? [];
      const total = first.totalCount;
      const single = firstItems.length === 0 || firstItems.length < pageSize || total === undefined || pageSize <= 0;
      const pageCount = single ? 1 : Math.ceil((total ?? 0) / pageSize);
      if (pageCount <= 1) {
        return of([firstItems]);
      }

      const rest$ = from(Array.from({ length: pageCount - 1 }, (_, i) => i + 2)).pipe(
        mergeMap(
          pageIndex => fetchPage(pageIndex).pipe(map(page => ({ pageIndex, items: page.results ?? [] }))),
          Math.max(1, concurrency),
        ),
        toArray(),
        map(pages => pages.sort((a, b) => a.pageIndex - b.pageIndex).map(p => p.items)),
      );
      return rest$.pipe(map(restItems => [firstItems, ...restItems]));
    }),
    map(pages => {
      const seen = new Set<unknown>();
      const items: T[] = [];
      for (const page of pages) {
        for (const item of page) {
          const id = getId(item);
          if (!seen.has(id)) {
            seen.add(id);
            items.push(item);
          }
        }
      }
      return items;
    }),
  );
}
