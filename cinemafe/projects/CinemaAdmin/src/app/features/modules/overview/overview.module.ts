import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from 'CinemaLib';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

import { DashboardComponent } from './dashboard/dashboard.component';
import { ReportsComponent } from './reports/reports.component';
import { MoviesManagementComponent } from './movies/movies-management.component';
import { MovieFormComponent } from './movies/movie-form.component';
import { TheatersManagementComponent } from './theaters/theaters-management.component';
import { TheaterFormComponent } from './theaters/theater-form.component';
import { TheaterDetailComponent } from './theaters/theater-detail.component';
import { TheaterRoomsComponent } from './theaters/theater-rooms.component';
import { TheaterRoomTypesComponent } from './theaters/theater-room-types.component';
import { TheaterSeatTypesComponent } from './theaters/theater-seat-types.component';
import { TheaterFoodComponent } from './theaters/theater-food.component';
import { TheaterTimeSlotsComponent } from './theaters/theater-time-slots.component';
import { TheaterTicketPricesComponent } from './theaters/theater-ticket-prices.component';
import { ShowTimesManagementComponent } from './show-times/show-times.component';
import { UsersManagementComponent } from './users/users-management.component';
import { UserFormComponent } from './users/user-form.component';

/**
 * Overview admin pages — dashboard, reports, movies, theaters, showtimes and
 * users. One module instead of one .module.ts per page, since each page
 * shares the same imports. Each page's component folder lives alongside this
 * module (not under the old per-feature folders under features/).
 *
 * Routing note: `app.routes.ts` has ONE pass-through entry (`path: ''`) that
 * loads this module; the routes below list each page's real, distinct path —
 * this module owns all of them, so Angular resolves straight to the matching
 * one instead of backtracking to a sibling.
 */
const routes: Routes = [
  { path: 'dashboard', component: DashboardComponent },
  { path: 'reports', component: ReportsComponent },
  { path: 'movies', component: MoviesManagementComponent },
  { path: 'theaters', component: TheatersManagementComponent },
  { path: 'theaters/:id', component: TheaterDetailComponent },
  { path: 'showtimes', component: ShowTimesManagementComponent },
  { path: 'users', component: UsersManagementComponent },
];

@NgModule({
  declarations: [
    DashboardComponent,
    ReportsComponent,
    MoviesManagementComponent, MovieFormComponent,
    TheatersManagementComponent, TheaterFormComponent, TheaterDetailComponent,
    TheaterRoomsComponent, TheaterRoomTypesComponent, TheaterSeatTypesComponent,
    TheaterFoodComponent, TheaterTimeSlotsComponent, TheaterTicketPricesComponent,
    ShowTimesManagementComponent,
    UsersManagementComponent, UserFormComponent,
  ],
  imports: [SharedModule, ModalComponent, ConfirmModalComponent, RouterModule.forChild(routes)],
})
export class OverviewModule {}
