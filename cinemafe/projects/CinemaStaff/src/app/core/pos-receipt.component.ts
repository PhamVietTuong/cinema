import { Component, ViewEncapsulation, computed, input, output, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { PriceBreakdownAdjustment, PriceBreakdownComponent, PriceBreakdownLine, CinemaServiceAgent, ServerUtcPipe, TicketCardComponent } from 'CinemaLib';

/** What the print view needs after a sale: the API result plus the showtime context captured at sale time. */
export interface SaleReceipt {
  result: CinemaServiceAgent.CounterSaleResultDTO;
  movieTitle: string;
  roomName: string;
  showTime?: Date;
  theaterName: string;
}

/**
 * Receipt and ticket print view shown after a sale. `window.print()` prints only `.pos-print-area`
 * (the global print rules below hide the rest of the page); the paper format toggles between A6 and an 80 mm roll.
 */
@Component({
  selector: 'staff-pos-receipt',
  standalone: true,
  encapsulation: ViewEncapsulation.None,
  imports: [DatePipe, DecimalPipe, TranslatePipe, MatButtonModule, MatButtonToggleModule, MatIconModule, TicketCardComponent, PriceBreakdownComponent, ServerUtcPipe],
  templateUrl: './pos-receipt.component.html',
  styleUrl: './pos-receipt.component.scss',
})
export class PosReceiptComponent {
  readonly receipt = input.required<SaleReceipt>();
  readonly newSale = output<void>();
  /** Heading, and label / icon of the closing button (the exchange dialog reuses the view with its own wording). */
  readonly titleKey = input('pos.receipt.done');
  readonly doneKey = input('pos.receipt.newSale');
  readonly doneIcon = input('add_shopping_cart');

  protected readonly format = signal<'a6' | 'roll'>('a6');

  protected readonly lines = computed<PriceBreakdownLine[]>(() =>
    (this.receipt().result.lines ?? []).map(l => ({
      label: l.description ?? '',
      quantity: l.quantity,
      amount: l.lineTotal ?? 0,
    })));

  protected readonly adjustments = computed<PriceBreakdownAdjustment[]>(() => {
    const r = this.receipt().result;
    return [
      { labelKey: 'pos.quote.discount', amount: r.discountAmount ?? 0 },
      { labelKey: 'pos.quote.points', amount: r.pointsValue ?? 0 },
      { labelKey: 'pos.quote.giftCard', amount: r.giftCardAmount ?? 0 },
    ];
  });

  protected print(): void {
    window.print();
  }
}
