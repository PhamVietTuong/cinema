import { Routes } from '@angular/router';
import { APPROVER_ROLES, roleGuard } from 'CinemaLib';

/** Tasks, mounted at /tasks: "my tasks" for every staff role, the assignment board for approvers. */
export const TASKS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./my-tasks.component').then(m => m.MyTasksComponent),
  },
  {
    path: 'board',
    canActivate: [roleGuard(APPROVER_ROLES)],
    loadComponent: () => import('./task-board.component').then(m => m.TaskBoardComponent),
  },
];
