import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { SharedModule } from 'CinemaLib';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';

import { AgeRestrictionsManagementComponent } from './age-restrictions/age-restrictions.component';
import { AgeRestrictionDialog } from './age-restrictions/age-restriction.dialog';
import { MovieTypesManagementComponent } from './movie-types/movie-types.component';
import { MovieTypeDialog } from './movie-types/movie-type.dialog';
import { DiscountTypesManagementComponent } from './discount-types/discount-types.component';
import { DiscountTypeDialog } from './discount-types/discount-type.dialog';
import { MembershipsManagementComponent } from './memberships/memberships.component';
import { MembershipDialog } from './memberships/membership.dialog';
import { UserTypesManagementComponent } from './user-types/user-types.component';
import { UserTypeDialog } from './user-types/user-type.dialog';
import { HolidaysManagementComponent } from './holidays/holidays.component';
import { NewsManagementComponent } from './news/news.component';
import { NewsDialog } from './news/news.dialog';
import { PeopleManagementComponent } from './people/people.component';
import { PersonDialog } from './people/person.dialog';
import { TemplatesManagementComponent } from './templates/templates.component';
import { TemplateDialog } from './templates/template.dialog';

const routes: Routes = [
  { path: 'age-restrictions', component: AgeRestrictionsManagementComponent },
  { path: 'movie-types', component: MovieTypesManagementComponent },
  { path: 'discount-types', component: DiscountTypesManagementComponent },
  { path: 'memberships', component: MembershipsManagementComponent },
  { path: 'user-types', component: UserTypesManagementComponent },
  { path: 'holidays', component: HolidaysManagementComponent },
  { path: 'news', component: NewsManagementComponent },
  { path: 'people', component: PeopleManagementComponent },
  { path: 'templates', component: TemplatesManagementComponent },
];

@NgModule({
  declarations: [
    AgeRestrictionsManagementComponent,
    AgeRestrictionDialog,
    MovieTypesManagementComponent,
    MovieTypeDialog,
    DiscountTypesManagementComponent,
    DiscountTypeDialog,
    MembershipsManagementComponent,
    MembershipDialog,
    UserTypesManagementComponent,
    UserTypeDialog,
    HolidaysManagementComponent,
    NewsManagementComponent,
    NewsDialog,
    PeopleManagementComponent,
    PersonDialog,
    TemplatesManagementComponent,
    TemplateDialog,
  ],
  imports: [
    SharedModule,
    NgxDatatableModule,
    MatCheckboxModule,
    RouterModule.forChild(routes),
  ],
})
export class CatalogAdminModule {}
