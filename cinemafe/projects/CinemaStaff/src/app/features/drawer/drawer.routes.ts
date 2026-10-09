import { Routes } from '@angular/router';

/** Cash drawer page for sellers. Mounted behind roleGuard(SELLER_ROLES) via a pass-through entry in app.routes.ts. */
export const DRAWER_ROUTES: Routes = [
  { path: 'drawer', loadComponent: () => import('./drawer.component').then(m => m.DrawerComponent) },
];
