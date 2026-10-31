import { firstValueFrom, of } from 'rxjs';
import { fetchAllPages, FetchablePage } from './paging';

interface Row { id: number; }

function makePages(total: number, pageSize: number): FetchablePage<Row>[] {
  const pages: FetchablePage<Row>[] = [];
  for (let start = 0; start < total; start += pageSize) {
    const results: Row[] = [];
    for (let i = start; i < Math.min(start + pageSize, total); i++) {
      results.push({ id: i });
    }
    pages.push({ results, totalCount: total });
  }
  return pages;
}

describe('fetchAllPages', () => {
  it('loops until the total is collected (450 rows, pageSize 200 -> 3 calls)', async () => {
    const pages = makePages(450, 200);
    let calls = 0;
    const items = await firstValueFrom(
      fetchAllPages<Row>(pageIndex => { calls++; return of(pages[pageIndex - 1]); }, 200, r => r.id),
    );
    expect(calls).toBe(3);
    expect(items.length).toBe(450);
  });

  it('stops after a single call when the total is 0', async () => {
    let calls = 0;
    const items = await firstValueFrom(
      fetchAllPages<Row>(() => { calls++; return of({ results: [], totalCount: 0 }); }, 200, r => r.id),
    );
    expect(calls).toBe(1);
    expect(items).toEqual([]);
  });

  it('stops exactly at a page boundary without an extra request (400 rows, pageSize 200)', async () => {
    const pages = makePages(400, 200);
    let calls = 0;
    const items = await firstValueFrom(
      fetchAllPages<Row>(pageIndex => { calls++; return of(pages[pageIndex - 1]); }, 200, r => r.id),
    );
    expect(calls).toBe(2);
    expect(items.length).toBe(400);
  });

  it('stops if a page returns fewer rows than totalCount suggests remain', async () => {
    let calls = 0;
    const responses: FetchablePage<Row>[] = [
      { results: [{ id: 1 }, { id: 2 }], totalCount: 500 },
      { results: [], totalCount: 500 },
    ];
    const items = await firstValueFrom(
      fetchAllPages<Row>(pageIndex => { calls++; return of(responses[pageIndex - 1]); }, 200, r => r.id),
    );
    expect(calls).toBe(1);
    expect(items.length).toBe(2);
  });

  it('stops after page 1 when totalCount is missing', async () => {
    let calls = 0;
    const items = await firstValueFrom(
      fetchAllPages<Row>(() => { calls++; return of({ results: [{ id: 1 }] }); }, 200, r => r.id),
    );
    expect(calls).toBe(1);
    expect(items.length).toBe(1);
  });

  it('de-duplicates rows with the same id across pages', async () => {
    const responses: FetchablePage<Row>[] = [
      { results: [{ id: 1 }, { id: 2 }], totalCount: 3 },
      { results: [{ id: 2 }, { id: 3 }], totalCount: 3 },
    ];
    const items = await firstValueFrom(
      fetchAllPages<Row>(pageIndex => of(responses[pageIndex - 1]), 2, r => r.id),
    );
    expect(items.map(r => r.id)).toEqual([1, 2, 3]);
  });
});
