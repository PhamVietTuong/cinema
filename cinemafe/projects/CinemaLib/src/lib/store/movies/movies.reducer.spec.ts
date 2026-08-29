import { moviesReducer } from './movies.reducer';
import { initialMoviesState, MoviesState } from './movies.state';
import * as MoviesActions from './movies.actions';
import { Movie, PagedResult } from '../../models/movie.models';

const movie = (id: number, title: string): Movie => ({
  id,
  title,
  description: '',
  duration: 120,
  releaseDate: '2026-07-01',
  ageRestrictionCode: 'P',
  genres: ['Action'],
  averageRating: 4,
  ratingCount: 10,
  isActive: true,
  isNowShowing: true,
  isComingSoon: false,
});

describe('moviesReducer', () => {
  it('load actions set loading and clear the previous error', () => {
    const from: MoviesState = { ...initialMoviesState, error: 'previous failure' };

    for (const action of [
      MoviesActions.loadNowShowing(),
      MoviesActions.loadMovies({ page: 1, pageSize: 20 }),
      MoviesActions.loadMovieDetail({ id: '1' }),
    ]) {
      const state = moviesReducer(from, action);
      expect(state.loading).toBe(true);
      expect(state.error).toBeNull();
    }
  });

  it('loadNowShowingSuccess replaces the list and stops loading', () => {
    const movies = [movie(1, 'Dune'), movie(2, 'Arrival')];

    const state = moviesReducer({ ...initialMoviesState, loading: true },
      MoviesActions.loadNowShowingSuccess({ movies }));

    expect(state.nowShowing).toEqual(movies);
    expect(state.loading).toBe(false);
  });

  it('loadComingSoonSuccess fills comingSoon without touching nowShowing', () => {
    const showing = [movie(1, 'Dune')];
    const soon = [movie(9, 'Sequel')];
    const from: MoviesState = { ...initialMoviesState, nowShowing: showing };

    const state = moviesReducer(from, MoviesActions.loadComingSoonSuccess({ movies: soon }));

    expect(state.comingSoon).toEqual(soon);
    expect(state.nowShowing).toEqual(showing);
  });

  it('loadMoviesSuccess stores the paged result', () => {
    const result: PagedResult<Movie> = {
      items: [movie(1, 'Dune')],
      total: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasPrevious: false,
      hasNext: false,
    };

    const state = moviesReducer(initialMoviesState, MoviesActions.loadMoviesSuccess({ result }));

    expect(state.pagedMovies).toEqual(result);
    expect(state.loading).toBe(false);
  });

  it('failure actions surface the error and stop loading', () => {
    const from: MoviesState = { ...initialMoviesState, loading: true };

    for (const action of [
      MoviesActions.loadNowShowingFailure({ error: 'network' }),
      MoviesActions.loadMoviesFailure({ error: 'network' }),
      MoviesActions.loadMovieDetailFailure({ error: 'network' }),
    ]) {
      const state = moviesReducer(from, action);
      expect(state.error).toBe('network');
      expect(state.loading).toBe(false);
    }
  });

  it('an unrelated action returns the same state reference', () => {
    const state = moviesReducer(initialMoviesState, { type: '[Other] Noop' } as never);

    expect(state).toBe(initialMoviesState);
  });
});
