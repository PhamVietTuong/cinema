import { Routes } from '@angular/router';

/** Ticket gate for GATE_KEEPER_ROLES, mounted at /gate behind roleGuard in app.routes.ts. */
export const GATE_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./gate-page.component').then(m => m.GatePageComponent) },
];
