import { Routes } from '@angular/router';
import { APPROVER_ROLES, roleGuard } from 'CinemaLib';

/** Roster, mounted at /roster for approvers only. */
export const ROSTER_ROUTES: Routes = [
  {
    path: '',
    canActivate: [roleGuard(APPROVER_ROLES)],
    loadComponent: () => import('./roster.component').then(m => m.RosterComponent),
  },
];
