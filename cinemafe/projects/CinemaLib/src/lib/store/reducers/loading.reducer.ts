import { createReducer, on } from '@ngrx/store';
import { LoadingActions } from '../actions';

export const loadingReducer = createReducer(
  false,

  on(LoadingActions.showLoading, () => {
    return true;
  }),

  on(LoadingActions.hideLoading, () => {
    return false;
  })
);