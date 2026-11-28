import { ActivatedRouteSnapshot, BaseRouteReuseStrategy } from '@angular/router';

/**
 * Default reuse, except the movie detail page (`:slug`) is rebuilt when only the slug changes —
 * the page reads its route param once on init, so navigating detail -> detail (a "similar movie"
 * card, a search suggestion) must create a fresh component rather than reuse the stale one.
 */
export class CinemaRouteReuseStrategy extends BaseRouteReuseStrategy {
  override shouldReuseRoute(future: ActivatedRouteSnapshot, curr: ActivatedRouteSnapshot): boolean {
    if (future.routeConfig !== curr.routeConfig) {
      return false;
    }
    if (future.routeConfig?.path === ':slug') {
      return future.params['slug'] === curr.params['slug'];
    }
    return true;
  }
}
