import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, mergeMap, map, switchMap } from 'rxjs/operators';
import { of } from 'rxjs';
import { CinemaServiceAgent } from '../../services/cinema-http.service';
import { apiErrorMessage } from '../../services/api-error';
import * as MoviesActions from './movies.actions';

@Injectable()
export class MoviesEffects {
  private actions$ = inject(Actions);
  private _cinemaService = inject(CinemaServiceAgent.HttpService);

  loadNowShowing$ = createEffect(() =>
    this.actions$.pipe(
      ofType(MoviesActions.loadNowShowing),
      switchMap(({ page, pageSize }) =>
        this._cinemaService.getNowShowingMovies(
          CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: page, pageSize })
        ).pipe(
          map(r => MoviesActions.loadNowShowingSuccess({ movies: (r.results ?? []) as any, total: r.totalCount ?? 0, page })),
          catchError(err => of(MoviesActions.loadNowShowingFailure({ error: err.message })))
        )
      )
    )
  );

  loadComingSoon$ = createEffect(() =>
    this.actions$.pipe(
      ofType(MoviesActions.loadComingSoon),
      switchMap(({ page, pageSize }) =>
        this._cinemaService.getComingSoonMovies(
          CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: page, pageSize })
        ).pipe(
          map(r => MoviesActions.loadComingSoonSuccess({ movies: (r.results ?? []) as any, total: r.totalCount ?? 0, page })),
          catchError(err => of(MoviesActions.loadComingSoonFailure({ error: err.message })))
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
          mergeMap(() => of(
            MoviesActions.rateMovieSuccess({ movieId, score }),
            MoviesActions.loadMovieDetail({ id: movieId })
          )),
          catchError(err => of(MoviesActions.rateMovieFailure({ error: apiErrorMessage(err, err?.message ?? '') })))
        )
      )
    )
  );

  addComment$ = createEffect(() =>
    this.actions$.pipe(
      ofType(MoviesActions.addComment),
      switchMap(({ movieId, content, parentId }) =>
        this._cinemaService.addComment(CinemaServiceAgent.AddCommentRequest.fromJS({ content, parentId }), movieId).pipe(
          mergeMap(() => of(
            MoviesActions.addCommentSuccess({ movieId }),
            MoviesActions.loadMovieDetail({ id: movieId })
          )),
          catchError(err => of(MoviesActions.addCommentFailure({ error: apiErrorMessage(err, err?.message ?? '') })))
        )
      )
    )
  );
}
