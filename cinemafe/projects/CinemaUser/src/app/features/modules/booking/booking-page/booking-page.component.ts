import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SharedModule } from 'CinemaLib';
import { BookingSelectionComponent } from '../booking-selection/booking-selection.component';
import { BookingCheckoutState } from '../booking-checkout/booking-checkout.state';

/**
 * Routed wrapper around BookingSelectionComponent (ticket quantities, seat map, snacks) — a thin
 * page header/breadcrumb plus reading showTimeId/roomId off the query string. The same selection
 * component is also embedded inline on the movie-detail page; this route remains as a direct deep
 * link and as the target BookingCheckoutComponent navigates back to from "change seats".
 */
@Component({
  selector: 'app-booking-page',
  standalone: true,
  imports: [SharedModule, BookingSelectionComponent],
  templateUrl: './booking-page.component.html',
  styleUrl: './booking-page.component.scss'
})
export class BookingPageComponent implements OnInit {
  showTimeId = '';
  roomId = '';
  /** Order carried back via BookingCheckoutComponent's "back to seats", when it came from here. */
  restoreState?: BookingCheckoutState;

  constructor(private _route: ActivatedRoute) {}

  ngOnInit(): void {
    this.showTimeId = this._route.snapshot.queryParams['showTimeId'] ?? '';
    this.roomId = this._route.snapshot.queryParams['roomId'] ?? '';
    this.restoreState = (history.state as { restore?: BookingCheckoutState } | undefined)?.restore;
  }
}
