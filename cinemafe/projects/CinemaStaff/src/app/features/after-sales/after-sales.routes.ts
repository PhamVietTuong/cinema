import { Routes } from '@angular/router';

/** After-sales desk (refund, exchange, reprint) for SELLER_ROLES, mounted at /after-sales behind roleGuard in app.routes.ts. */
export const AFTER_SALES_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./after-sales.component').then(m => m.AfterSalesComponent) },
];
