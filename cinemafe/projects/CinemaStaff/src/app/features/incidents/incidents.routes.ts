import { Routes } from '@angular/router';

/** Incidents, mounted at /incidents for every staff role (blocking needs an approver or a PIN override, enforced by the API). */
export const INCIDENTS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./incident-list.component').then(m => m.IncidentListComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./incident-detail.component').then(m => m.IncidentDetailComponent),
  },
];
