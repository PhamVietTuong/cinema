import { Routes } from '@angular/router';

/** Counter point of sale for sellers. Mounted behind roleGuard(SELLER_ROLES) via a pass-through entry in app.routes.ts. */
export const POS_ROUTES: Routes = [
  { path: 'pos', loadComponent: () => import('./pos.component').then(m => m.PosComponent) },
];
