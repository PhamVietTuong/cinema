import { Routes } from '@angular/router';
import { authGuard } from 'CinemaLib';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent)
  },
  { path: 'login', redirectTo: 'auth/login', pathMatch: 'full' },
  {
    path: 'auth',
    children: [
      {
        path: 'login',
        loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
      },
      {
        path: 'register',
        loadComponent: () => import('./features/auth/register/register.component').then(m => m.RegisterComponent)
      }
    ]
  },
  {
    path: 'movies',
    children: [
      {
        path: '',
        loadComponent: () => import('./features/movies/movie-list/movie-list.component').then(m => m.MovieListComponent)
      },
      {
        path: ':id',
        loadComponent: () => import('./features/movies/movie-detail/movie-detail.component').then(m => m.MovieDetailComponent)
      }
    ]
  },
  {
    path: 'booking',
    canActivate: [authGuard],
    children: [
      {
        path: 'seats',
        loadComponent: () => import('./features/booking/seat-selection/seat-selection.component').then(m => m.SeatSelectionComponent)
      },
      {
        path: 'confirmation',
        loadComponent: () => import('./features/booking/booking-confirmation/booking-confirmation.component').then(m => m.BookingConfirmationComponent)
      }
    ]
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile.component').then(m => m.ProfileComponent)
  },
  { path: '**', redirectTo: '' }
];
