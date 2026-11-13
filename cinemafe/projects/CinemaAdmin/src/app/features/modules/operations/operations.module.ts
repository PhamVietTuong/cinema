import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FilterBarComponent, SharedModule, StatusPillComponent } from 'CinemaLib';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';

import { InvoicesManagementComponent } from './invoices/invoices.component';
import { InvoiceStatusDialog } from './invoices/invoice-status.dialog';
import { CommentsModerationComponent } from './comments/comments.component';
import { AuditLogManagementComponent } from './audit-log/audit-log.component';
import { PromotionProgramsManagementComponent } from './promotion-programs/promotion-programs.component';
import { PromotionProgramDialog } from './promotion-programs/promotion-program.dialog';
import { SplashBannersManagementComponent } from './splash-banners/splash-banners.component';
import { SplashBannerDialog } from './splash-banners/splash-banner.dialog';

/**
 * "Vận Hành" (Operations) admin pages: promotion programs, invoices, comment
 * moderation, audit log. Each page's component folder lives
 * alongside this module. See categories/catalog-admin.module.ts for the
 * routing convention this follows (one pass-through entry in app.routes.ts,
 * this module's own routes list each page's real path).
 */
const routes: Routes = [
  { path: 'invoices', component: InvoicesManagementComponent },
  { path: 'comments', component: CommentsModerationComponent },
  { path: 'audit-log', component: AuditLogManagementComponent },
  { path: 'promotion-programs', component: PromotionProgramsManagementComponent },
  { path: 'splash-banners', component: SplashBannersManagementComponent },
];

@NgModule({
  declarations: [
    InvoicesManagementComponent,
    InvoiceStatusDialog,
    CommentsModerationComponent,
    AuditLogManagementComponent,
    PromotionProgramsManagementComponent,
    PromotionProgramDialog,
    SplashBannersManagementComponent,
    SplashBannerDialog,
  ],
  imports: [
    SharedModule,
    NgxDatatableModule,
    MatCheckboxModule,
    StatusPillComponent,
    FilterBarComponent,
    RouterModule.forChild(routes),
  ],
})
export class OperationsModule {}
