import { createAction, props } from '@ngrx/store';

export const saveSearchState = createAction(
  '[Search] saveSearchState',
  props<{searchState: any}>()
);

