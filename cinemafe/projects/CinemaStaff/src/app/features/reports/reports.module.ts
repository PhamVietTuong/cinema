import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuditLogComponent } from './audit-log/audit-log.component';

/** Management reports for REPORTING_ROLES, mounted behind roleGuard in app.routes.ts. */
const routes: Routes = [
  { path: 'audit-log', component: AuditLogComponent },
];

@NgModule({
  imports: [AuditLogComponent, RouterModule.forChild(routes)],
})
export class ReportsModule {}
