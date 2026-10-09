import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { QrCodeComponent } from '../qr/qr-code.component';

/** What one ticket shows. Structurally matches the API's TicketItemDTO. */
export interface TicketCardData {
  seatLabel?: string;
  seatType?: string;
  patronCategory?: string;
  price?: number;
  /** Value encoded in the QR code; the QR is omitted when absent. */
  qrCode?: string | null;
}

/**
 * One admission ticket with its QR code, for on-screen display and for printing (`print` switches to a
 * black-on-white compact layout sized for an A6 sheet or an 80 mm roll).
 * Usage: `<cl-ticket-card [ticket]="t" movieTitle="..." [showTime]="..." roomName="..." invoiceCode="..." [print]="true" />`
 */
@Component({
  selector: 'cl-ticket-card',
  standalone: true,
  imports: [DatePipe, DecimalPipe, TranslatePipe, QrCodeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="cl-ticket" [class.cl-ticket--print]="print()">
      <header class="cl-ticket__head">
        @if (theaterName()) {
          <span class="cl-ticket__theater">{{ theaterName() }}</span>
        }
        <h3 class="cl-ticket__movie">{{ movieTitle() }}</h3>
      </header>

      <div class="cl-ticket__body">
        <dl class="cl-ticket__facts">
          @if (showTime()) {
            <div><dt>{{ 'ticket.showTime' | translate }}</dt><dd>{{ showTime() | date:'dd/MM/yyyy HH:mm' }}</dd></div>
          }
          @if (roomName()) {
            <div><dt>{{ 'ticket.room' | translate }}</dt><dd>{{ roomName() }}</dd></div>
          }
          <div><dt>{{ 'ticket.seat' | translate }}</dt><dd class="cl-ticket__seat">{{ ticket().seatLabel }}</dd></div>
          @if (ticket().patronCategory) {
            <div><dt>{{ 'ticket.category' | translate }}</dt><dd>{{ ticket().patronCategory }}</dd></div>
          }
          @if (ticket().price !== undefined && ticket().price !== null) {
            <div><dt>{{ 'ticket.price' | translate }}</dt><dd>{{ ticket().price | number:'1.0-0' }}đ</dd></div>
          }
        </dl>
        @if (ticket().qrCode) {
          <cl-qr-code class="cl-ticket__qr" [value]="ticket().qrCode" [size]="print() ? 120 : 132" />
        }
      </div>

      @if (invoiceCode()) {
        <footer class="cl-ticket__foot">{{ 'ticket.order' | translate }} {{ invoiceCode() }}</footer>
      }
    </article>
  `,
  styles: [`
    :host { display: block; }
    .cl-ticket {
      border: 1px solid var(--ml-rule, #d8d2c4); border-radius: 10px; overflow: hidden;
      background: var(--ml-panel, #fff); color: var(--ml-ink, #1c1917);
    }
    .cl-ticket__head { padding: 12px 16px; background: var(--ml-action-soft, #f6ead0); border-bottom: 1px dashed var(--ml-rule, #d8d2c4); }
    .cl-ticket__theater { display: block; font-size: 0.7rem; letter-spacing: 0.12em; text-transform: uppercase; color: var(--ml-muted, #777); }
    .cl-ticket__movie { margin: 2px 0 0; font-size: 1.05rem; line-height: 1.25; }
    .cl-ticket__body { display: flex; gap: 12px; align-items: center; justify-content: space-between; padding: 12px 16px; }
    .cl-ticket__facts { margin: 0; display: grid; gap: 4px; font-size: 0.85rem; }
    .cl-ticket__facts div { display: flex; gap: 8px; }
    .cl-ticket__facts dt { min-width: 64px; color: var(--ml-muted, #777); }
    .cl-ticket__facts dd { margin: 0; font-weight: 600; }
    .cl-ticket__seat { font-size: 1.2rem; }
    .cl-ticket__foot { padding: 8px 16px; font-size: 0.72rem; color: var(--ml-muted, #777); border-top: 1px dashed var(--ml-rule, #d8d2c4); }

    /* Printed layout: black on white, no tints, narrow enough for an 80 mm roll. */
    .cl-ticket--print { max-width: 72mm; border: 1px solid #000; border-radius: 0; background: #fff; color: #000; }
    .cl-ticket--print .cl-ticket__head { background: #fff; border-bottom: 1px dashed #000; padding: 6px 8px; }
    .cl-ticket--print .cl-ticket__theater, .cl-ticket--print .cl-ticket__facts dt, .cl-ticket--print .cl-ticket__foot { color: #000; }
    .cl-ticket--print .cl-ticket__body { padding: 6px 8px; }
    .cl-ticket--print .cl-ticket__foot { border-top: 1px dashed #000; padding: 4px 8px; }
  `],
})
export class TicketCardComponent {
  readonly ticket = input.required<TicketCardData>();
  readonly movieTitle = input('');
  readonly theaterName = input('');
  readonly roomName = input('');
  readonly showTime = input<Date | string | null | undefined>(null);
  readonly invoiceCode = input('');
  /** Print layout (black on white, narrow). */
  readonly print = input(false);
}
