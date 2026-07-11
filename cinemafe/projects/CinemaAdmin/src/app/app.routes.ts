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
        path: 'profile',
        loadComponent: () => import('./features/profile/profile.component').then(m => m.ProfileComponent)
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
        path: 'theaters/:id',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/theaters/theater-detail.component').then(m => m.TheaterDetailComponent)
      },
      {
        path: 'showtimes',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/catalog/show-times/show-times.component').then(m => m.ShowTimesManagementComponent)
      },
      {
        path: 'discounts',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/catalog/discounts/discounts.component').then(m => m.DiscountsManagementComponent)
      },
      {
        path: 'invoices',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/catalog/invoices/invoices.component').then(m => m.InvoicesManagementComponent)
      },
      {
        path: 'users',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/users/users-management.component').then(m => m.UsersManagementComponent)
      },

      // ── Catalog (lookup) management ──────────────────────────────────────────
      {
        path: 'movie-types',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/catalog/movie-types/movie-types.component').then(m => m.MovieTypesManagementComponent)
      },
      {
        path: 'age-restrictions',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/catalog/age-restrictions/age-restrictions.component').then(m => m.AgeRestrictionsManagementComponent)
      },
      {
        path: 'discount-types',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/catalog/discount-types/discount-types.component').then(m => m.DiscountTypesManagementComponent)
      },
      {
        path: 'memberships',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/catalog/memberships/memberships.component').then(m => m.MembershipsManagementComponent)
      },
      {
        path: 'user-types',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/catalog/user-types/user-types.component').then(m => m.UserTypesManagementComponent)
      },
      {
        path: 'holidays',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/catalog/holidays/holidays.component').then(m => m.HolidaysManagementComponent)
      },
      {
        path: 'news',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/catalog/news/news.component').then(m => m.NewsManagementComponent)
      }
    ]
  },
  // Fallback: send unknown URLs straight to login
  { path: '**', redirectTo: 'auth/login' }
];
