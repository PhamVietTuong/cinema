import { Routes } from '@angular/router';

/** Food pickup queue for CONCESSION_ROLES, mounted at /kitchen behind roleGuard in app.routes.ts. */
export const KITCHEN_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./kitchen.component').then(m => m.KitchenComponent) },
];
