import { Routes } from '@angular/router';

/**
 * Warehouse pages for all back-office roles (Admin, TheaterStaff):
 * inventory and storage (restock) plans. Mounted behind roleGuard(BACK_OFFICE_ROLES)
 * via one pass-through entry in app.routes.ts. Every page is scoped to the theater from
 * TheaterContextService.
 */
export const WAREHOUSE_ROUTES: Routes = [
  { path: 'inventory', loadComponent: () => import('./inventory/inventory-list.component').then(m => m.InventoryListComponent) },
  { path: 'storage-plans', loadComponent: () => import('./storage-plans/storage-plan-list.component').then(m => m.StoragePlanListComponent) },
  { path: 'storage-plans/:id', loadComponent: () => import('./storage-plans/storage-plan-detail.component').then(m => m.StoragePlanDetailComponent) },
];
