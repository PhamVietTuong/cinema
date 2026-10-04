import { Routes } from '@angular/router';
import { authGuard, roleGuard, homeRedirectByRole, BACK_OFFICE_ROLES, STAFF_APP_ROLES, UserRoles } from 'CinemaLib';

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
      },
      // Operations and workforce: one lazy route file per feature; approver-only screens guard themselves.
      { path: 'schedule', loadChildren: () => import('./features/schedule/schedule.routes').then(m => m.SCHEDULE_ROUTES) },
      { path: 'incidents', loadChildren: () => import('./features/incidents/incidents.routes').then(m => m.INCIDENTS_ROUTES) },
      { path: 'checklists', loadChildren: () => import('./features/checklists/checklists.routes').then(m => m.CHECKLISTS_ROUTES) },
      { path: 'roster', loadChildren: () => import('./features/roster/roster.routes').then(m => m.ROSTER_ROUTES) },
      { path: 'time-clock', loadChildren: () => import('./features/time-clock/time-clock.routes').then(m => m.TIME_CLOCK_ROUTES) },
      { path: 'tasks', loadChildren: () => import('./features/tasks/tasks.routes').then(m => m.TASKS_ROUTES) },
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
