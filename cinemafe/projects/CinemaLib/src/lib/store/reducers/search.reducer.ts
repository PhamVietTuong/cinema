import { Action, createReducer, on } from '@ngrx/store';
import { SearchActions } from '../actions';

export const searchReducer = createReducer(
  null,

  on(SearchActions.saveSearchState, (state, action) => {
    return Object.assign({}, action.searchState);
  }),
);