import { Routes } from '@angular/router';

/** Drawer close and daily close for SELLER_ROLES, mounted at /cash-close behind roleGuard in app.routes.ts. */
export const CASH_CLOSE_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./cash-close-page.component').then(m => m.CashClosePageComponent) },
];
