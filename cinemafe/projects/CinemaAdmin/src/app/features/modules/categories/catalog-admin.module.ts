import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from 'CinemaLib';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

import { AgeRestrictionsManagementComponent } from './age-restrictions/age-restrictions.component';
import { AgeRestrictionDialog } from './age-restrictions/age-restriction.dialog';
import { MovieTypesManagementComponent } from './movie-types/movie-types.component';
import { DiscountTypesManagementComponent } from './discount-types/discount-types.component';
import { MembershipsManagementComponent } from './memberships/memberships.component';
import { UserTypesManagementComponent } from './user-types/user-types.component';
import { HolidaysManagementComponent } from './holidays/holidays.component';
import { NewsManagementComponent } from './news/news.component';

/**
 * "Danh Mục" (Categories) admin pages — simple lookup entities: age
 * restrictions, movie types, discount types, membership tiers, user types,
 * holidays, news. One module instead of one .module.ts per page, since each
 * page/dialog is small and they all share the same imports. Each page's
 * component folder lives alongside this module (not under features/catalog/).
 *
 * Routing note: `app.routes.ts` has ONE pass-through entry (`path: ''`) that
 * loads this module; the routes below list each page's real, distinct path —
 * this module owns all of them, so Angular resolves straight to the matching
 * one instead of backtracking to a sibling.
 */
const routes: Routes = [
  { path: 'age-restrictions', component: AgeRestrictionsManagementComponent },
  { path: 'movie-types', component: MovieTypesManagementComponent },
  { path: 'discount-types', component: DiscountTypesManagementComponent },
  { path: 'memberships', component: MembershipsManagementComponent },
  { path: 'user-types', component: UserTypesManagementComponent },
  { path: 'holidays', component: HolidaysManagementComponent },
  { path: 'news', component: NewsManagementComponent },
];

@NgModule({
  declarations: [
    AgeRestrictionsManagementComponent, AgeRestrictionDialog,
    MovieTypesManagementComponent,
    DiscountTypesManagementComponent,
    MembershipsManagementComponent,
    UserTypesManagementComponent,
    HolidaysManagementComponent,
    NewsManagementComponent,
  ],
  imports: [SharedModule, NgxDatatableModule, ModalComponent, ConfirmModalComponent, RouterModule.forChild(routes)],
})
export class CatalogAdminModule {}
