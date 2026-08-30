import { createAction, props } from '@ngrx/store';

export const showSuccess = createAction(
  '[Notification] showSuccess',
  props<{message?: string}>()
);

export const showError = createAction(
  '[Notification] showError',
  props<{message?: string}>()
);

export const showException = createAction(
  '[Notification] showException',
  props<{error?: any}>()
);