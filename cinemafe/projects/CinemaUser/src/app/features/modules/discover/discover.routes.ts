import { Routes } from '@angular/router';

/** Public browse/marketing pages: home, movies, theaters, promotions, membership. */
export const DISCOVER_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/home.component').then(m => m.HomeComponent)
  },
  {
    path: 'movies',
    children: [
      {
        path: '',
        loadComponent: () => import('./movies/movie-list/movie-list.component').then(m => m.MovieListComponent)
      },
      {
        // Legacy links (from before friendly slugs): drop the trailing title slug and fall through
        // to the ':slug' route below, which detects the GUID and loads by id instead.
        path: ':legacyId/:legacySlug',
        redirectTo: '/movies/:legacyId'
      },
      {
        path: ':slug',
        loadComponent: () => import('./movies/movie-detail/movie-detail.component').then(m => m.MovieDetailComponent)
      }
    ]
  },
  {
    path: 'theaters',
    loadComponent: () => import('./theaters/theaters.component').then(m => m.TheatersComponent)
  },
  {
    path: 'promotions',
    loadComponent: () => import('./promotions/promotions.component').then(m => m.PromotionsComponent)
  },
  {
    path: 'membership',
    loadComponent: () => import('./membership/membership.component').then(m => m.MembershipComponent)
  }
];
