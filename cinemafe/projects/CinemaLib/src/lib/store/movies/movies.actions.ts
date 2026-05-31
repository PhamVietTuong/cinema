import { createAction, props } from '@ngrx/store';
import { Movie, MovieDetail, PagedResult } from '../../models/movie.models';

export const loadNowShowing = createAction('[Movies] Load Now Showing');
export const loadNowShowingSuccess = createAction('[Movies] Load Now Showing Success', props<{ movies: Movie[] }>());
export const loadNowShowingFailure = createAction('[Movies] Load Now Showing Failure', props<{ error: string }>());

export const loadComingSoon = createAction('[Movies] Load Coming Soon');
export const loadComingSoonSuccess = createAction('[Movies] Load Coming Soon Success', props<{ movies: Movie[] }>());

export const loadMovies = createAction('[Movies] Load Movies', props<{ search?: string; genreId?: number; page: number; pageSize: number }>());
export const loadMoviesSuccess = createAction('[Movies] Load Movies Success', props<{ result: PagedResult<Movie> }>());
export const loadMoviesFailure = createAction('[Movies] Load Movies Failure', props<{ error: string }>());

export const loadMovieDetail = createAction('[Movies] Load Movie Detail', props<{ id: string }>());
export const loadMovieDetailSuccess = createAction('[Movies] Load Movie Detail Success', props<{ movie: MovieDetail }>());
export const loadMovieDetailFailure = createAction('[Movies] Load Movie Detail Failure', props<{ error: string }>());
