import { Routes } from '@angular/router';
import { authGuard, roleGuard, homeRedirectByRole, BACK_OFFICE_ROLES, GATE_KEEPER_ROLES, REPORTING_ROLES, STAFF_APP_ROLES, UserRoles } from 'CinemaLib';

/**
 * Where each role lands from '/'. Everyone goes to /home until the role-specific pages
 * (gate, POS, kitchen) exist; add them here as they are built.
 */
const STAFF_LANDING: Record<string, string> = {
  [UserRoles.Admin]: '/home',
  [UserRoles.TheaterManager]: '/home',
  [UserRoles.TheaterStaff]: '/home',
  [UserRoles.RegionalManager]: '/home',
  [UserRoles.BoxOfficeStaff]: '/home',
  [UserRoles.KitchenStaff]: '/home',
  [UserRoles.GateStaff]: '/gate',
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
      },
      {
        path: 'profile',
        loadComponent: () => import('./features/profile/profile.component').then(m => m.ProfileComponent)
      },
      // Gate: ticket scanning and lookup.
      {
        path: 'gate',
        canActivate: [roleGuard(GATE_KEEPER_ROLES)],
        loadChildren: () => import('./features/gate/gate.module').then(m => m.GateModule)
      },
      // Reports (audit log): management roles. The module's own routes match /audit-log.
      {
        path: '',
        canActivate: [roleGuard(REPORTING_ROLES)],
        loadChildren: () => import('./features/reports/reports.module').then(m => m.ReportsModule)
      },
      // Warehouse (inventory + storage plans): back-office roles. One pass-through entry loads WarehouseModule,
      // whose own routes match /inventory, /storage-plans and /storage-plans/:id.
      {
        path: '',
        canActivate: [roleGuard(BACK_OFFICE_ROLES)],
        loadChildren: () => import('./features/warehouse/warehouse.module').then(m => m.WarehouseModule)
      }
    ]
  },
  // Fallback: send unknown URLs straight to login
  { path: '**', redirectTo: 'auth/login' }
];
