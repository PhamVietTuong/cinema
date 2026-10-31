import { moviesReducer } from './movies.reducer';
import { initialMoviesState, MoviesState } from './movies.state';
import * as MoviesActions from './movies.actions';
import { Movie, PagedResult } from '../../models/movie.models';

const movie = (id: number, title: string): Movie => ({
  id,
  title,
  slug: `movie-${id}`,
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
      MoviesActions.loadNowShowing({ page: 1, pageSize: 15 }),
      MoviesActions.loadComingSoon({ page: 1, pageSize: 15 }),
      MoviesActions.loadMovies({ page: 1, pageSize: 20 }),
      MoviesActions.loadMovieDetail({ id: '1' }),
      MoviesActions.loadMovieDetailBySlug({ slug: 'movie-1' }),
    ]) {
      const state = moviesReducer(from, action);
      expect(state.loading).toBe(true);
      expect(state.error).toBeNull();
    }
  });

  it('loadNowShowingSuccess replaces the list on page 1 and stops loading', () => {
    const movies = [movie(1, 'Dune'), movie(2, 'Arrival')];

    const state = moviesReducer({ ...initialMoviesState, loading: true },
      MoviesActions.loadNowShowingSuccess({ movies, total: movies.length, page: 1 }));

    expect(state.nowShowing).toEqual(movies);
    expect(state.nowShowingTotal).toBe(movies.length);
    expect(state.loading).toBe(false);
  });

  it('loadNowShowingSuccess appends to the list on page 2', () => {
    const first = [movie(1, 'Dune')];
    const second = [movie(2, 'Arrival')];
    const from: MoviesState = { ...initialMoviesState, nowShowing: first, nowShowingTotal: 2 };

    const state = moviesReducer(from, MoviesActions.loadNowShowingSuccess({ movies: second, total: 2, page: 2 }));

    expect(state.nowShowing).toEqual([...first, ...second]);
    expect(state.nowShowingTotal).toBe(2);
  });

  it('loadComingSoonSuccess fills comingSoon without touching nowShowing', () => {
    const showing = [movie(1, 'Dune')];
    const soon = [movie(9, 'Sequel')];
    const from: MoviesState = { ...initialMoviesState, nowShowing: showing };

    const state = moviesReducer(from, MoviesActions.loadComingSoonSuccess({ movies: soon, total: soon.length, page: 1 }));

    expect(state.comingSoon).toEqual(soon);
    expect(state.comingSoonTotal).toBe(soon.length);
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
      MoviesActions.loadComingSoonFailure({ error: 'network' }),
      MoviesActions.loadMoviesFailure({ error: 'network' }),
      MoviesActions.loadMovieDetailFailure({ error: 'network' }),
    ]) {
      const state = moviesReducer(from, action);
      expect(state.error).toBe('network');
      expect(state.loading).toBe(false);
    }
  });

  it('loadMovieDetailFailure clears the previously selected movie', () => {
    const from: MoviesState = { ...initialMoviesState, selectedMovie: movie(1, 'Wicked') as never };

    const state = moviesReducer(from, MoviesActions.loadMovieDetailFailure({ error: 'not found' }));

    expect(state.selectedMovie).toBeNull();
    expect(state.error).toBe('not found');
  });

  it('an unrelated action returns the same state reference', () => {
    const state = moviesReducer(initialMoviesState, { type: '[Other] Noop' } as never);

    expect(state).toBe(initialMoviesState);
  });
});
