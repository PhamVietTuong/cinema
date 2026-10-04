import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SalesReportsComponent } from './sales-reports.component';

/** Sales, occupancy and KPI reports for REPORTING_ROLES, mounted at /reports behind roleGuard in app.routes.ts. */
const routes: Routes = [
  { path: '', component: SalesReportsComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
})
export class SalesReportsModule {}
