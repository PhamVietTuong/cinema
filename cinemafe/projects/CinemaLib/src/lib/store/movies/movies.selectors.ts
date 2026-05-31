import { createFeatureSelector, createSelector } from '@ngrx/store';
import { MoviesState } from './movies.state';

export const selectMoviesState = createFeatureSelector<MoviesState>('movies');
export const selectNowShowing = createSelector(selectMoviesState, s => s.nowShowing);
export const selectComingSoon = createSelector(selectMoviesState, s => s.comingSoon);
export const selectPagedMovies = createSelector(selectMoviesState, s => s.pagedMovies);
export const selectSelectedMovie = createSelector(selectMoviesState, s => s.selectedMovie);
export const selectMoviesLoading = createSelector(selectMoviesState, s => s.loading);
