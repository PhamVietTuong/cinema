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

// Reviews & comments (re-load the detail on success to refresh the list)
export const rateMovie = createAction('[Movies] Rate Movie', props<{ movieId: string; score: number; review?: string }>());
export const rateMovieFailure = createAction('[Movies] Rate Movie Failure', props<{ error: string }>());

export const addComment = createAction('[Movies] Add Comment', props<{ movieId: string; content: string; parentId?: string }>());
export const addCommentFailure = createAction('[Movies] Add Comment Failure', props<{ error: string }>());
