import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';
import * as QRCode from 'qrcode';

/**
 * Renders `value` as a QR code image (data URL, so it also prints reliably).
 * Usage: `<cl-qr-code [value]="ticket.qrCode" [size]="160" />`
 */
@Component({
  selector: 'cl-qr-code',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (dataUrl(); as url) {
      <img class="cl-qr" [src]="url" [alt]="alt()" [width]="size()" [height]="size()">
    }
  `,
  styles: [`
    :host { display: inline-block; line-height: 0; }
    .cl-qr { image-rendering: pixelated; background: #fff; }
  `],
})
export class QrCodeComponent {
  /** Text encoded in the code. Empty renders nothing. */
  readonly value = input<string | null | undefined>('');
  /** Edge length in CSS pixels. */
  readonly size = input(160);
  /** Quiet-zone width in modules. */
  readonly margin = input(1);
  /** Accessible text of the image. */
  readonly alt = input('QR');

  protected readonly dataUrl = signal('');
  private readonly _text = computed(() => (this.value() ?? '').trim());
  private _seq = 0;

  constructor() {
    effect(() => {
      const text = this._text();
      const width = this.size();
      const margin = this.margin();
      const seq = ++this._seq;
      if (!text) {
        this.dataUrl.set('');
        return;
      }
      QRCode.toDataURL(text, { margin, width })
        .then(url => {
          if (seq === this._seq) {
            this.dataUrl.set(url);
          }
        })
        .catch(() => {
          if (seq === this._seq) {
            this.dataUrl.set('');
          }
        });
    });
  }
}
