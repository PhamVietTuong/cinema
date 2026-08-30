import { Action, createReducer, on } from '@ngrx/store';
import { LoadingActions } from '../actions';

export const loadingReducer = createReducer(
  false,

  on(LoadingActions.showLoading, (state, action) => {
    return true;
  }),

  on(LoadingActions.hideLoading, (state, action) => {
    return false;
  })
);