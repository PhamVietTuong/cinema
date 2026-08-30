import { Routes } from '@angular/router';
import { authGuard, adminGuard } from 'CinemaLib';

export const routes: Routes = [
  // Convenience aliases so /login and /auth/login both work
  { path: 'login', redirectTo: 'auth/login', pathMatch: 'full' },
  {
    path: 'auth',
    children: [
      {
        path: 'login',
        loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
      }
    ]
  },
  // Where adminGuard sends non-admins. Must stay outside the authGuard tree below,
  // whose root redirects to /dashboard — routing there instead would loop forever.
  {
    path: 'forbidden',
    loadComponent: () => import('./features/forbidden/forbidden.component').then(m => m.ForbiddenComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'profile',
        loadComponent: () => import('./features/profile/profile.component').then(m => m.ProfileComponent)
      },

      // ── Overview: dashboard, reports, movies, theaters, showtimes, users ────────
      // All 7 pages below share one lazy module (OverviewModule) — this single
      // pass-through entry (`path: ''`) loads it once; the module's OWN routes
      // array then matches the real segment (dashboard, movies, theaters, ...).
      {
        path: '',
        canActivate: [adminGuard],
        loadChildren: () => import('./features/modules/overview/overview.module').then(m => m.OverviewModule)
      },

      // ── Catalog (lookup) management ──────────────────────────────────────────
      // All 7 pages below share one lazy module (CatalogAdminModule) — this single
      // pass-through entry (`path: ''`) loads it once; the module's OWN routes
      // array then matches the real segment (age-restrictions, movie-types, ...).
      // Angular backtracks to the next sibling below if the module doesn't own
      // the requested segment, so ordering here doesn't affect correctness.
      {
        path: '',
        canActivate: [adminGuard],
        loadChildren: () => import('./features/modules/categories/catalog-admin.module').then(m => m.CatalogAdminModule)
      },
      // ── Operations ─────────────────────────────────────────────────────────────
      {
        path: '',
        canActivate: [adminGuard],
        loadChildren: () => import('./features/modules/operations/operations.module').then(m => m.OperationsModule)
      }
    ]
  },
  // Fallback: send unknown URLs straight to login
  { path: '**', redirectTo: 'auth/login' }
];
