import { test, expect } from '@playwright/test';
import { PAGING } from '../../helpers/api';

test.describe('API: health and public catalog', () => {
  test('liveness endpoint is up', async ({ request }) => {
    const res = await request.get('/health');
    expect(res.status()).toBe(200);
  });

  test('GetMovies returns a paged result', async ({ request }) => {
    const res = await request.post('/api/Cinema/GetMovies', { data: PAGING });
    expect(res.status()).toBe(200);
    expect(await res.json()).toEqual(expect.objectContaining({ items: expect.any(Array) }));
  });

  test('GetNowShowingMovies returns a paged result', async ({ request }) => {
    const res = await request.post('/api/Cinema/GetNowShowingMovies', { data: PAGING });
    expect(res.status()).toBe(200);
  });

  test('GetTheaters returns a paged result', async ({ request }) => {
    const res = await request.post('/api/Cinema/GetTheaters', { data: PAGING });
    expect(res.status()).toBe(200);
    expect(await res.json()).toEqual(expect.objectContaining({ items: expect.any(Array) }));
  });

  test('GetMovieLanguages returns a list', async ({ request }) => {
    const res = await request.get('/api/Cinema/GetMovieLanguages');
    expect(res.status()).toBe(200);
  });

  test('rejects an out-of-range page size', async ({ request }) => {
    const res = await request.post('/api/Cinema/GetMovies', { data: { pageIndex: 1, pageSize: 5000 } });
    expect(res.status()).toBe(400);
  });
});
