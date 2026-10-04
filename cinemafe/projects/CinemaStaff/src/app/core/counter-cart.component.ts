import { Component, inject, input } from '@angular/core';
import { SeatMapComponent, SharedModule } from 'CinemaLib';
import { CounterCartService } from './counter-cart.service';

/**
 * Showtime picker, seat map with the ticket list and the food / combo picker of the counter. It renders the
 * `CounterCartService` provided by the host (POS page, exchange dialog); `busy` freezes the seat map during a submit.
 */
@Component({
  selector: 'staff-counter-cart',
  standalone: true,
  imports: [SharedModule, SeatMapComponent],
  templateUrl: './counter-cart.component.html',
  styleUrl: './counter-cart.component.scss',
})
export class CounterCartComponent {
  protected readonly cart = inject(CounterCartService);

  readonly busy = input(false);
  /** Offer the "food only" pseudo showtime (the POS does, an exchange does not). */
  readonly allowFnbOnly = input(true);
}
