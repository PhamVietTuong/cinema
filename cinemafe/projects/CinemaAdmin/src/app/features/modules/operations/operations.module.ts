import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FilterBarComponent, SharedModule, StatusPillComponent } from 'CinemaLib';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';

import { DiscountsManagementComponent } from './discounts/discounts.component';
import { DiscountDialog } from './discounts/discount.dialog';
import { InvoicesManagementComponent } from './invoices/invoices.component';
import { InvoiceStatusDialog } from './invoices/invoice-status.dialog';
import { CommentsModerationComponent } from './comments/comments.component';
import { GiftCardsManagementComponent } from './gift-cards/gift-cards.component';
import { GiftCardDialog } from './gift-cards/gift-card.dialog';
import { AuditLogManagementComponent } from './audit-log/audit-log.component';

/**
 * "Vận Hành" (Operations) admin pages: discount codes, invoices, comment
 * moderation, gift cards, audit log. Each page's component folder lives
 * alongside this module. See categories/catalog-admin.module.ts for the
 * routing convention this follows (one pass-through entry in app.routes.ts,
 * this module's own routes list each page's real path).
 */
const routes: Routes = [
  { path: 'discounts', component: DiscountsManagementComponent },
  { path: 'invoices', component: InvoicesManagementComponent },
  { path: 'comments', component: CommentsModerationComponent },
  { path: 'gift-cards', component: GiftCardsManagementComponent },
  { path: 'audit-log', component: AuditLogManagementComponent },
];

@NgModule({
  declarations: [
    DiscountsManagementComponent,
    DiscountDialog,
    InvoicesManagementComponent,
    InvoiceStatusDialog,
    CommentsModerationComponent,
    GiftCardsManagementComponent,
    GiftCardDialog,
    AuditLogManagementComponent,
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
