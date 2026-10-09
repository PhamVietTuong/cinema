import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { PriceBreakdownComponent, SharedModule } from 'CinemaLib';
import { CounterCartService } from './counter-cart.service';

/** Member, discount / gift-card codes and the server-quoted totals of the counter cart. */
@Component({
  selector: 'staff-counter-summary',
  standalone: true,
  imports: [SharedModule, MatProgressBarModule, PriceBreakdownComponent],
  templateUrl: './counter-summary.component.html',
  styleUrl: './counter-summary.component.scss',
})
export class CounterSummaryComponent {
  protected readonly cart = inject(CounterCartService);
}
