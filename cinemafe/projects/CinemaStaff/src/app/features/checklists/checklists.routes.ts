import { Routes } from '@angular/router';
import { APPROVER_ROLES, roleGuard } from 'CinemaLib';

/** Checklists, mounted at /checklists for every staff role; template editing is for approvers only. */
export const CHECKLISTS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./checklists-home.component').then(m => m.ChecklistsHomeComponent),
  },
  {
    path: 'run/:showTimeId/:roomId',
    loadComponent: () => import('./checklist-run.component').then(m => m.ChecklistRunComponent),
  },
  {
    path: 'templates',
    canActivate: [roleGuard(APPROVER_ROLES)],
    loadComponent: () => import('./checklist-templates.component').then(m => m.ChecklistTemplatesComponent),
  },
];
