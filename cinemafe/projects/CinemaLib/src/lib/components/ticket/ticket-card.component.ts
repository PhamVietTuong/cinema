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
  templateUrl: './ticket-card.component.html',
  styleUrl: './ticket-card.component.scss',
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
