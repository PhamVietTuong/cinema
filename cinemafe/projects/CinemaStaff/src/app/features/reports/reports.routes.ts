import { Routes } from '@angular/router';

/** Management reports for REPORTING_ROLES, mounted behind roleGuard in app.routes.ts. */
export const REPORTS_ROUTES: Routes = [
  { path: 'audit-log', loadComponent: () => import('./audit-log/audit-log.component').then(m => m.AuditLogComponent) },
];
