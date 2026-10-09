import { Routes } from '@angular/router';

/** Sales, occupancy and KPI reports for REPORTING_ROLES, mounted at /reports behind roleGuard in app.routes.ts. */
export const SALES_REPORTS_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./sales-reports.component').then(m => m.SalesReportsComponent) },
];
