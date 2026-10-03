import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from 'CinemaLib';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';

import { DashboardComponent } from './dashboard/dashboard.component';
import { ReportsComponent } from './reports/reports.component';
import { MoviesManagementComponent } from './movies/movies-management.component';
import { MovieDialog } from './movies/movie.dialog';
import { TheatersManagementComponent } from './theaters/theaters-management.component';
import { TheaterDialog } from './theaters/theater.dialog';
import { TheaterDetailComponent } from './theaters/theater-detail.component';
import { TheaterRoomsComponent } from './theaters/theater-rooms.component';
import { RoomDialog } from './theaters/room.dialog';
import { SeatMapDialog } from './theaters/seat-map.dialog';
import { TheaterRoomTypesComponent } from './theaters/theater-room-types.component';
import { RoomTypeDialog } from './theaters/room-type.dialog';
import { RoomTypePricesDialog } from './theaters/room-type-prices.dialog';
import { TheaterSeatTypesComponent } from './theaters/theater-seat-types.component';
import { SeatTypeDialog } from './theaters/seat-type.dialog';
import { TheaterPatronCategoriesComponent } from './theaters/theater-patron-categories.component';
import { PatronCategoryDialog } from './theaters/patron-category.dialog';
import { TheaterFoodComponent } from './theaters/theater-food.component';
import { TheaterCombosComponent } from './theaters/theater-combos.component';
import { FoodAndDrinkDialog } from './theaters/food-and-drink.dialog';
import { TheaterTimeSlotsComponent } from './theaters/theater-time-slots.component';
import { TimeSlotDialog } from './theaters/time-slot.dialog';
import { TheaterTicketPricesComponent } from './theaters/theater-ticket-prices.component';
import { TicketPriceDialog } from './theaters/ticket-price.dialog';
import { ShowTimesManagementComponent } from './show-times/show-times.component';
import { ShowTimeDialog } from './show-times/show-time.dialog';
import { UsersManagementComponent } from './users/users-management.component';
import { UserDialog } from './users/user.dialog';

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
    MoviesManagementComponent,
    MovieDialog,
    TheatersManagementComponent,
    TheaterDialog,
    TheaterDetailComponent,
    TheaterRoomsComponent,
    RoomDialog,
    SeatMapDialog,
    TheaterRoomTypesComponent,
    RoomTypeDialog,
    RoomTypePricesDialog,
    TheaterSeatTypesComponent,
    SeatTypeDialog,
    TheaterPatronCategoriesComponent,
    PatronCategoryDialog,
    TheaterFoodComponent,
    TheaterCombosComponent,
    FoodAndDrinkDialog,
    TheaterTimeSlotsComponent,
    TimeSlotDialog,
    TheaterTicketPricesComponent,
    TicketPriceDialog,
    ShowTimesManagementComponent,
    ShowTimeDialog,
    UsersManagementComponent,
    UserDialog,
  ],
  imports: [
    SharedModule,
    NgxDatatableModule,
    RouterModule.forChild(routes),
  ],
})
export class OverviewModule {}
