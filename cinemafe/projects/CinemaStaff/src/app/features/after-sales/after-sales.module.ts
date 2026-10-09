import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AfterSalesComponent } from './after-sales.component';

/** After-sales desk (refund, exchange, reprint) for SELLER_ROLES, mounted at /after-sales behind roleGuard in app.routes.ts. */
const routes: Routes = [
  { path: '', component: AfterSalesComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
})
export class AfterSalesModule {}
