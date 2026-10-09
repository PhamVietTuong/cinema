import { Routes } from '@angular/router';
import { authGuard } from 'CinemaLib';

/**
 * Feature groups, mirroring CinemaAdmin's domain-module convention: auth and profile stay flat,
 * everything else is grouped under features/modules/<domain>/.
 */
export const routes: Routes = [
  { path: 'login', redirectTo: 'auth/login', pathMatch: 'full' },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES)
  },
  {
    path: 'booking',
    canActivate: [authGuard],
    loadChildren: () => import('./features/modules/booking/booking.routes').then(m => m.BOOKING_ROUTES)
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile.component').then(m => m.ProfileComponent)
  },
  // Public browse/marketing pages (home, movies, theaters, promotions, membership): pass-through,
  // must stay after every specific segment above.
  {
    path: '',
    loadChildren: () => import('./features/modules/discover/discover.routes').then(m => m.DISCOVER_ROUTES)
  },
  { path: '**', redirectTo: '' }
];
