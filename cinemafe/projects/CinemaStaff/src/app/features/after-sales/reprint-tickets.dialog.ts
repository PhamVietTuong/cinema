import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import * as QRCode from 'qrcode';
import { SharedModule, StaffServiceAgent } from 'CinemaLib';
import { buildTicketsPrintHtml } from './ticket-print';

export interface ReprintTicketsDialogData {
  result: StaffServiceAgent.ReprintResultDTO;
}

/** Shows the reprinted tickets with their QR codes and prints them (A6 / 80 mm layout in a print window). */
@Component({
  selector: 'staff-reprint-tickets-dialog',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<div mat-dialog-title class="dialog-title">{{ 'afterSales.reprint.ticketsTitle' | translate: { code: data.result.invoiceCode } }}</div>
<mat-dialog-content>
  <p class="meta">{{ data.result.movieTitle }} &middot; {{ data.result.roomName }} &middot; {{ data.result.showStart | date: 'HH:mm dd/MM/yyyy' }}</p>
  <div class="tickets">
    @for (ticket of data.result.tickets ?? []; track $index) {
      <div class="ticket">
        @if (qr[$index]; as src) {
          <img [src]="src" width="120" height="120" [alt]="'afterSales.reprint.qrAlt' | translate">
        }
        <strong>{{ ticket.seatLabel }}</strong>
        <span class="meta">{{ ticket.seatType }}</span>
      </div>
    }
  </div>
</mat-dialog-content>
<div mat-dialog-actions class="dialog-actions">
  <button mat-raised-button type="button" (click)="close()">{{ 'common.close' | translate }}</button>
  <button mat-raised-button color="primary" type="button" (click)="print()">
    <mat-icon>print</mat-icon> {{ 'afterSales.reprint.print' | translate }}
  </button>
</div>
`,
  styles: [`
    .meta { color: var(--ml-muted, #777); font-size: 13px; }
    .tickets { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 8px; }
    .ticket { display: flex; flex-direction: column; align-items: center; gap: 4px; padding: 8px 12px; border: 1px solid var(--ml-panel-3, rgba(0, 0, 0, 0.12)); border-radius: 8px; }
  `],
})
export class ReprintTicketsDialogComponent implements OnInit {
  readonly data = inject<ReprintTicketsDialogData>(MAT_DIALOG_DATA);
  private readonly _ref = inject(MatDialogRef<ReprintTicketsDialogComponent>);
  private readonly _cdr = inject(ChangeDetectorRef);
  private readonly _translate = inject(TranslateService);

  /** QR image (data URL) per ticket, same order as `result.tickets`. */
  qr: string[] = [];

  ngOnInit(): void {
    const tickets = this.data.result.tickets ?? [];
    Promise.all(tickets.map(t => (t.qrCode ? QRCode.toDataURL(t.qrCode, { margin: 1, width: 240 }) : Promise.resolve(''))))
      .then(images => {
        this.qr = images;
        this._cdr.markForCheck();
      });
  }

  print(): void {
    const win = window.open('', '_blank', 'width=420,height=640');
    if (!win) {
      return;
    }
    win.document.write(buildTicketsPrintHtml(this.data.result, this.qr, {
      title: this._translate.instant('afterSales.reprint.printTitle', { code: this.data.result.invoiceCode }),
    }));
    win.document.close();
    win.focus();
    win.print();
  }

  close(): void {
    this._ref.close();
  }
}
