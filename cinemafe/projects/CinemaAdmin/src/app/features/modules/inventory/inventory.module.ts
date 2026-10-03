import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from 'CinemaLib';

import { InventoryListComponent } from './inventory/inventory-list.component';
import { StoragePlanListComponent } from './storage-plans/storage-plan-list.component';
import { StoragePlanDetailComponent } from './storage-plans/storage-plan-detail.component';

/**
 * Warehouse pages for all back-office roles (Admin, TheaterManager, TheaterStaff):
 * inventory and storage (restock) plans. Mounted behind roleGuard(BACK_OFFICE_ROLES)
 * via one pass-through entry in app.routes.ts.
 */
const routes: Routes = [
  { path: 'inventory', component: InventoryListComponent },
  { path: 'storage-plans', component: StoragePlanListComponent },
  { path: 'storage-plans/:id', component: StoragePlanDetailComponent },
];

@NgModule({
  imports: [
    SharedModule,
    InventoryListComponent,
    StoragePlanListComponent,
    StoragePlanDetailComponent,
    RouterModule.forChild(routes),
  ],
})
export class InventoryModule {}
