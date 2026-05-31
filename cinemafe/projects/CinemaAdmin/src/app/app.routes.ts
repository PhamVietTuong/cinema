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
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'movies',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/movies/movies-management.component').then(m => m.MoviesManagementComponent)
      },
      {
        path: 'theaters',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/theaters/theaters-management.component').then(m => m.TheatersManagementComponent)
      },
      {
        path: 'showtimes',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/showtimes/showtimes-management.component').then(m => m.ShowtimesManagementComponent)
      },
      {
        path: 'users',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/users/users-management.component').then(m => m.UsersManagementComponent)
      }
    ]
  },
  // Fallback: send unknown URLs straight to login
  { path: '**', redirectTo: 'auth/login' }
];
