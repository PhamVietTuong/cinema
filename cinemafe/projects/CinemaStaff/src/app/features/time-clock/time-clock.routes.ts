import { Routes } from '@angular/router';

/** Time clock, mounted at /time-clock for every staff role (the time sheet card is shown to approvers only). */
export const TIME_CLOCK_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./time-clock.component').then(m => m.TimeClockComponent),
  },
];
