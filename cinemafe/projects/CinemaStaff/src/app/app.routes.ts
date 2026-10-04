import { Routes } from '@angular/router';
import { authGuard, roleGuard, homeRedirectByRole, STAFF_APP_ROLES, UserRoles } from 'CinemaLib';

/**
 * Where each role lands from '/'. Everyone goes to /home until the role-specific pages
 * (gate, POS, kitchen) exist; add them here as they are built.
 */
const STAFF_LANDING: Record<string, string> = {
  [UserRoles.Admin]: '/home',
  [UserRoles.TheaterManager]: '/home',
  [UserRoles.TheaterStaff]: '/home',
};

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
  // Where roleGuard sends users without a staff role. Must stay outside the guarded tree below,
  // otherwise redirecting here would be guarded again and loop forever.
  {
    path: 'forbidden',
    loadComponent: () => import('./features/forbidden/forbidden.component').then(m => m.ForbiddenComponent)
  },
  {
    path: '',
    canActivate: [authGuard, roleGuard(STAFF_APP_ROLES)],
    children: [
      { path: '', pathMatch: 'full', canActivate: [homeRedirectByRole(STAFF_LANDING)], children: [] },
      {
        path: 'home',
        loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent)
      }
    ]
  },
  // Fallback: send unknown URLs straight to login
  { path: '**', redirectTo: 'auth/login' }
];
