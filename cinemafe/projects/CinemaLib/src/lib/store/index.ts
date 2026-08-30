export * from './auth';
export * from './movies';

// Named (not `export *`) to avoid colliding with `./auth`'s authReducer — the legacy
// `./reducers`/`./actions`/`./selectors` folders predate the per-feature store convention
// and still declare their own (different) authReducer.
export { searchReducer } from './reducers/search.reducer';
export { saveSearchState } from './actions/search.actions';
export { selectSearchState } from './selectors/search.selector';
export { showSuccess, showError, showException } from './actions/notification.actions';
export { NotificationEffects } from './effects/notification.effects';
export { showLoading, hideLoading } from './actions/loading.actions';
export { loadingReducer } from './reducers/loading.reducer';
export { selectLoading } from './selectors/loading.selector';
