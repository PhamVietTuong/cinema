import { Routes } from '@angular/router';

/** Schedule board, mounted at /schedule for every staff role. */
export const SCHEDULE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./schedule-board.component').then(m => m.ScheduleBoardComponent),
  },
];
