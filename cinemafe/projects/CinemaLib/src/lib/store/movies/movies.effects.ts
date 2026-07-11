import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, switchMap } from 'rxjs/operators';
import { of } from 'rxjs';
import { CinemaServiceAgent } from '../../services/cinema-http.service';
import * as MoviesActions from './movies.actions';

@Injectable()
export class MoviesEffects {
  private actions$ = inject(Actions);
  private _cinemaService = inject(CinemaServiceAgent.HttpService);

  loadNowShowing$ = createEffect(() =>
    this.actions$.pipe(
      ofType(MoviesActions.loadNowShowing),
      switchMap(() =>
        this._cinemaService.getNowShowingMovies(
          CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 50 })
        ).pipe(
          map(r => MoviesActions.loadNowShowingSuccess({ movies: (r.results ?? []) as any })),
          catchError(err => of(MoviesActions.loadNowShowingFailure({ error: err.message })))
        )
      )
    )
  );

  loadComingSoon$ = createEffect(() =>
    this.actions$.pipe(
      ofType(MoviesActions.loadComingSoon),
      switchMap(() =>
        this._cinemaService.getComingSoonMovies(
          CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 50 })
        ).pipe(
          map(r => MoviesActions.loadComingSoonSuccess({ movies: (r.results ?? []) as any })),
          catchError(() => of(MoviesActions.loadComingSoonSuccess({ movies: [] })))
        )
      )
    )
  );

  loadMovies$ = createEffect(() =>
    this.actions$.pipe(
      ofType(MoviesActions.loadMovies),
      switchMap(({ search, page, pageSize }) =>
        this._cinemaService.getMovies(
          CinemaServiceAgent.PagingSearchDTO.fromJS({
            pageIndex: page,
            pageSize,
            filters: search ? { search } : undefined,
          })
        ).pipe(
          map(r => MoviesActions.loadMoviesSuccess({
            result: {
              items: (r.results ?? []) as any,
              total: r.totalCount ?? 0,
              page,
              pageSize,
              totalPages: Math.ceil((r.totalCount ?? 0) / pageSize),
              hasPrevious: page > 1,
              hasNext: page * pageSize < (r.totalCount ?? 0),
            }
          })),
          catchError(err => of(MoviesActions.loadMoviesFailure({ error: err.message })))
        )
      )
    )
  );

  loadMovieDetail$ = createEffect(() =>
    this.actions$.pipe(
      ofType(MoviesActions.loadMovieDetail),
      switchMap(({ id }) =>
        this._cinemaService.getMovie(id).pipe(
          map(movie => MoviesActions.loadMovieDetailSuccess({ movie: movie as any })),
          catchError(err => of(MoviesActions.loadMovieDetailFailure({ error: err.message })))
        )
      )
    )
  );

  rateMovie$ = createEffect(() =>
    this.actions$.pipe(
      ofType(MoviesActions.rateMovie),
      switchMap(({ movieId, score, review }) =>
        this._cinemaService.rateMovie(CinemaServiceAgent.RateMovieRequest.fromJS({ score, review }), movieId).pipe(
          map(() => MoviesActions.loadMovieDetail({ id: movieId })),
          catchError(err => of(MoviesActions.rateMovieFailure({ error: err.message })))
        )
      )
    )
  );

  addComment$ = createEffect(() =>
    this.actions$.pipe(
      ofType(MoviesActions.addComment),
      switchMap(({ movieId, content, parentId }) =>
        this._cinemaService.addComment(CinemaServiceAgent.AddCommentRequest.fromJS({ content, parentId }), movieId).pipe(
          map(() => MoviesActions.loadMovieDetail({ id: movieId })),
          catchError(err => of(MoviesActions.addCommentFailure({ error: err.message })))
        )
      )
    )
  );
}
