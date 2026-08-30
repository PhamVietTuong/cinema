import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from 'CinemaLib';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

import { DiscountsManagementComponent } from './discounts/discounts.component';
import { InvoicesManagementComponent } from './invoices/invoices.component';
import { CommentsModerationComponent } from './comments/comments.component';
import { GiftCardsManagementComponent } from './gift-cards/gift-cards.component';

/**
 * "Vận Hành" (Operations) admin pages: discount codes, invoices, comment
 * moderation, gift cards. Each page's component folder lives alongside this
 * module. See categories/catalog-admin.module.ts for the routing convention
 * this follows (one pass-through entry in app.routes.ts, this module's own
 * routes list each page's real path).
 */
const routes: Routes = [
  { path: 'discounts', component: DiscountsManagementComponent },
  { path: 'invoices', component: InvoicesManagementComponent },
  { path: 'comments', component: CommentsModerationComponent },
  { path: 'gift-cards', component: GiftCardsManagementComponent },
];

@NgModule({
  declarations: [
    DiscountsManagementComponent,
    InvoicesManagementComponent,
    CommentsModerationComponent,
    GiftCardsManagementComponent,
  ],
  imports: [SharedModule, ModalComponent, ConfirmModalComponent, RouterModule.forChild(routes)],
})
export class OperationsModule {}
