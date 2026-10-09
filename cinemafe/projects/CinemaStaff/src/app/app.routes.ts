import { Routes } from '@angular/router';
import { ForbiddenPageComponent, authGuard, roleGuard, COMPLAINT_ROLES, homeRedirectByRole, BACK_OFFICE_ROLES, GATE_KEEPER_ROLES, REPORTING_ROLES, SELLER_ROLES, STAFF_APP_ROLES, UserRoles, CONCESSION_ROLES } from 'CinemaLib';

/**
 * Where each role lands from '/'. Everyone goes to /home until the role-specific pages
 * (gate, POS, kitchen) exist; add them here as they are built.
 */
const STAFF_LANDING: Record<string, string> = {
  [UserRoles.Admin]: '/home',
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
    component: ForbiddenPageComponent
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
        loadChildren: () => import('./features/gate/gate.routes').then(m => m.GATE_ROUTES)
      },
      // Kitchen: food pickup queue and low-stock alerts.
      {
        path: 'kitchen',
        canActivate: [roleGuard(CONCESSION_ROLES)],
        loadChildren: () => import('./features/kitchen/kitchen.routes').then(m => m.KITCHEN_ROUTES)
      },
      // Sales, occupancy and KPI reports: management roles.
      {
        path: 'reports',
        canActivate: [roleGuard(REPORTING_ROLES)],
        loadChildren: () => import('./features/sales-reports/sales-reports.routes').then(m => m.SALES_REPORTS_ROUTES)
      },
      // After-sales desk (refund, exchange, reprint) and cash close (drawer close, daily close): sellers.
      {
        path: 'after-sales',
        canActivate: [roleGuard(SELLER_ROLES)],
        loadChildren: () => import('./features/after-sales/after-sales.routes').then(m => m.AFTER_SALES_ROUTES)
      },
      {
        path: 'cash-close',
        canActivate: [roleGuard(SELLER_ROLES)],
        loadChildren: () => import('./features/cash-close/cash-close.routes').then(m => m.CASH_CLOSE_ROUTES)
      },
      // Customer service: lookup + e-ticket resend (sellers), complaints (sellers and regional managers); the routes guard themselves.
      {
        path: 'customer-service',
        canActivate: [roleGuard(COMPLAINT_ROLES)],
        loadChildren: () => import('./features/customer-service/customer-service.routes').then(m => m.CUSTOMER_SERVICE_ROUTES)
      },
      // Reports (audit log): management roles.
      {
        path: '',
        canActivate: [roleGuard(REPORTING_ROLES)],
        loadChildren: () => import('./features/reports/reports.routes').then(m => m.REPORTS_ROUTES)
      },
      // Operations and workforce: one lazy route file per feature; approver-only screens guard themselves.
      { path: 'schedule', loadChildren: () => import('./features/schedule/schedule.routes').then(m => m.SCHEDULE_ROUTES) },
      { path: 'incidents', loadChildren: () => import('./features/incidents/incidents.routes').then(m => m.INCIDENTS_ROUTES) },
      { path: 'checklists', loadChildren: () => import('./features/checklists/checklists.routes').then(m => m.CHECKLISTS_ROUTES) },
      { path: 'roster', loadChildren: () => import('./features/roster/roster.routes').then(m => m.ROSTER_ROUTES) },
      { path: 'time-clock', loadChildren: () => import('./features/time-clock/time-clock.routes').then(m => m.TIME_CLOCK_ROUTES) },
      { path: 'tasks', loadChildren: () => import('./features/tasks/tasks.routes').then(m => m.TASKS_ROUTES) },
      // Warehouse (inventory + storage plans): back-office roles. One pass-through entry loads its routes,
      // which match /inventory, /storage-plans and /storage-plans/:id.
      {
        path: '',
        canActivate: [roleGuard(BACK_OFFICE_ROLES)],
        loadChildren: () => import('./features/warehouse/warehouse.routes').then(m => m.WAREHOUSE_ROUTES)
      },
      // Counter POS (sellers).
      {
        path: '',
        canActivate: [roleGuard(SELLER_ROLES)],
        loadChildren: () => import('./features/pos/pos.routes').then(m => m.POS_ROUTES)
      },
      // Cash drawer (sellers).
      {
        path: '',
        canActivate: [roleGuard(SELLER_ROLES)],
        loadChildren: () => import('./features/drawer/drawer.routes').then(m => m.DRAWER_ROUTES)
      }
    ]
  },
  // Fallback: send unknown URLs straight to login
  { path: '**', redirectTo: 'auth/login' }
];
