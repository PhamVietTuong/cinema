import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import * as QRCode from 'qrcode';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { buildTicketsPrintHtml } from './ticket-print';

export interface ReprintTicketsDialogData {
  result: CinemaServiceAgent.ReprintResultDTO;
}

/** Shows the reprinted tickets with their QR codes and prints them (A6 / 80 mm layout in a print window). */
@Component({
  selector: 'staff-reprint-tickets-dialog',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reprint-tickets.dialog.html',
  styleUrl: './reprint-tickets.dialog.scss',
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
