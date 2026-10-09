import { Component, ViewEncapsulation, computed, input, output, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { PriceBreakdownAdjustment, PriceBreakdownComponent, PriceBreakdownLine, StaffServiceAgent, TicketCardComponent } from 'CinemaLib';

/** What the print view needs after a sale: the API result plus the showtime context captured at sale time. */
export interface SaleReceipt {
  result: StaffServiceAgent.CounterSaleResultDTO;
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
  imports: [DatePipe, DecimalPipe, TranslatePipe, MatButtonModule, MatButtonToggleModule, MatIconModule, TicketCardComponent, PriceBreakdownComponent],
  template: `
    <div class="pos-receipt-toolbar ad-card">
      <div>
        <h2 class="pos-receipt-title">{{ titleKey() | translate }}</h2>
        <p class="pos-receipt-code">{{ receipt().result.invoiceCode }}</p>
        <ng-content />
        @if ((receipt().result.changeDue ?? 0) > 0) {
          <p class="pos-receipt-change">{{ 'pos.receipt.changeDue' | translate }}: <strong>{{ receipt().result.changeDue | number:'1.0-0' }}đ</strong></p>
        }
      </div>
      <div class="pos-receipt-actions">
        <mat-button-toggle-group [value]="format()" (change)="format.set($event.value)" [attr.aria-label]="'pos.receipt.format' | translate">
          <mat-button-toggle value="a6">A6</mat-button-toggle>
          <mat-button-toggle value="roll">80 mm</mat-button-toggle>
        </mat-button-toggle-group>
        <button mat-raised-button color="primary" type="button" (click)="print()">
          <mat-icon>print</mat-icon> {{ 'pos.receipt.print' | translate }}
        </button>
        <button mat-raised-button type="button" (click)="newSale.emit()">
          <mat-icon>{{ doneIcon() }}</mat-icon> {{ doneKey() | translate }}
        </button>
      </div>
    </div>

    <div class="pos-print-area" [class.pos-print-area--roll]="format() === 'roll'">
      <section class="pos-print-sheet">
        <header class="pos-print-head">
          <strong>{{ receipt().theaterName }}</strong>
          <span>{{ receipt().result.paidAt | serverUtc | date:'dd/MM/yyyy HH:mm' }}</span>
          <span>{{ 'ticket.order' | translate }} {{ receipt().result.invoiceCode }}</span>
        </header>
        <cl-price-breakdown [lines]="lines()" [adjustments]="adjustments()" [total]="receipt().result.finalAmount ?? 0" />
        @if ((receipt().result.changeDue ?? 0) > 0) {
          <p class="pos-print-change">{{ 'pos.receipt.changeDue' | translate }}: {{ receipt().result.changeDue | number:'1.0-0' }}đ</p>
        }
      </section>

      @for (t of receipt().result.tickets ?? []; track $index) {
        <section class="pos-print-sheet pos-print-ticket">
          <cl-ticket-card [ticket]="t"
                          [movieTitle]="receipt().movieTitle"
                          [roomName]="receipt().roomName"
                          [showTime]="receipt().showTime"
                          [theaterName]="receipt().theaterName"
                          [invoiceCode]="receipt().result.invoiceCode ?? ''"
                          [print]="true" />
        </section>
      }
    </div>
  `,
  styles: [`
    .pos-receipt-toolbar { display: flex; justify-content: space-between; align-items: center; gap: 16px; flex-wrap: wrap; margin-bottom: 16px; }
    .pos-receipt-title { margin: 0; font-size: 1.2rem; }
    .pos-receipt-code { margin: 4px 0; color: var(--ml-muted); font-family: var(--ml-font-data); }
    .pos-receipt-change { margin: 4px 0; font-size: 1.1rem; }
    .pos-receipt-actions { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }

    .pos-print-area { display: grid; gap: 16px; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); }
    .pos-print-sheet { background: #fff; color: #000; border: 1px dashed var(--ml-rule); padding: 12px; }
    .pos-print-head { display: flex; flex-direction: column; gap: 2px; margin-bottom: 8px; font-size: 0.85rem; }
    .pos-print-change { margin: 8px 0 0; font-weight: 700; }
    .pos-print-area--roll .pos-print-sheet { max-width: 72mm; }

    @page { margin: 4mm; size: A6; }
    @media print {
      body * { visibility: hidden !important; }
      .pos-print-area, .pos-print-area * { visibility: visible !important; }
      .pos-print-area { position: absolute; left: 0; top: 0; width: 100%; display: block; }
      .pos-print-sheet { border: 0; padding: 0 0 6mm; break-after: page; page-break-after: always; }
      .pos-print-sheet:last-child { break-after: auto; page-break-after: auto; }
    }
  `],
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
