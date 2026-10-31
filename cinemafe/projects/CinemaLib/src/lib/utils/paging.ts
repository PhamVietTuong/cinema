import { EMPTY, Observable } from 'rxjs';
import { expand, map, reduce } from 'rxjs/operators';

/** The subset of a paged search result that `fetchAllPages` needs to decide whether to keep going. */
export interface FetchablePage<T> {
  results?: T[];
  totalCount?: number;
}

interface PageState<T> {
  items: T[];
  pageIndex: number;
  done: boolean;
}

/**
 * Repeatedly calls `fetchPage` (1-based page index) until every row of the underlying search has
 * been collected, then emits the combined array once. Stops when a page is empty, when a page comes
 * back shorter than `pageSize` (the last page), or when `totalCount` is missing (unknown total).
 * Rows are de-duplicated by `getId` as a safety net against an unstable sort on the server.
 */
export function fetchAllPages<T>(
  fetchPage: (pageIndex: number) => Observable<FetchablePage<T>>,
  pageSize: number,
  getId: (item: T) => unknown,
): Observable<T[]> {
  const toState = (page: FetchablePage<T>, pageIndex: number): PageState<T> => {
    const items = page.results ?? [];
    const collected = pageIndex * pageSize;
    const done = items.length === 0 || items.length < pageSize || page.totalCount === undefined || collected >= page.totalCount;
    return { items, pageIndex, done };
  };

  return fetchPage(1).pipe(
    map(page => toState(page, 1)),
    expand(state => state.done
      ? EMPTY
      : fetchPage(state.pageIndex + 1).pipe(map(page => toState(page, state.pageIndex + 1)))),
    reduce((acc, state) => {
      for (const item of state.items) {
        const id = getId(item);
        if (!acc.seen.has(id)) {
          acc.seen.add(id);
          acc.items.push(item);
        }
      }
      return acc;
    }, { items: [] as T[], seen: new Set<unknown>() }),
    map(acc => acc.items),
  );
}
