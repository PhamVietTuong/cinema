import { createReducer, on } from '@ngrx/store';
import { initialMoviesState } from './movies.state';
import * as MoviesActions from './movies.actions';

export const moviesReducer = createReducer(
  initialMoviesState,
  on(MoviesActions.loadNowShowing, MoviesActions.loadMovies, MoviesActions.loadMovieDetail,
    state => ({ ...state, loading: true, error: null })),
  on(MoviesActions.loadNowShowingSuccess, (state, { movies }) => ({ ...state, loading: false, nowShowing: movies })),
  on(MoviesActions.loadComingSoonSuccess, (state, { movies }) => ({ ...state, comingSoon: movies })),
  on(MoviesActions.loadMoviesSuccess, (state, { result }) => ({ ...state, loading: false, pagedMovies: result })),
  on(MoviesActions.loadMovieDetailSuccess, (state, { movie }) => ({ ...state, loading: false, selectedMovie: movie })),
  on(MoviesActions.loadNowShowingFailure, MoviesActions.loadMoviesFailure, MoviesActions.loadMovieDetailFailure,
    (state, { error }) => ({ ...state, loading: false, error })),
);
