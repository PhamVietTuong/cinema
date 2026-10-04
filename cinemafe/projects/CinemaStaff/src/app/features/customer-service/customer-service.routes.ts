import { Routes } from '@angular/router';
import { COMPLAINT_ROLES, SELLER_ROLES, roleGuard } from 'CinemaLib';

/**
 * Customer service, mounted at /customer-service (the lookup is /customer-service/lookup). The lookup (and e-ticket resend) is for sellers; the complaints screens
 * also admit regional managers, exactly as the API authorizes them.
 */
export const CUSTOMER_SERVICE_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'lookup' },
  {
    path: 'lookup',
    canActivate: [roleGuard(SELLER_ROLES)],
    loadComponent: () => import('./customer-lookup.component').then(m => m.CustomerLookupComponent),
  },
  {
    path: 'complaints',
    canActivate: [roleGuard(COMPLAINT_ROLES)],
    loadComponent: () => import('./complaint-list.component').then(m => m.ComplaintListComponent),
  },
  {
    path: 'complaints/:id',
    canActivate: [roleGuard(COMPLAINT_ROLES)],
    loadComponent: () => import('./complaint-detail.component').then(m => m.ComplaintDetailComponent),
  },
];
