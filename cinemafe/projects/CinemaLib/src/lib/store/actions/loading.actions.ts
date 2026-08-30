import { createAction, props } from '@ngrx/store';

export const showLoading = createAction(
  '[Loading] show'
);

export const hideLoading = createAction(
  '[Loading] hide'
);