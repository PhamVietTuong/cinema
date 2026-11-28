import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';
import { formatVnd } from '../utils/currency';

/**
 * Money in Vietnamese dong: `{{ invoice.finalAmount | vnd }}` renders `120,000đ` (separators follow the app locale).
 * Use this everywhere an amount is shown, so the three apps never format money differently. null/undefined render empty.
 */
@Pipe({ name: 'vnd', standalone: true })
export class VndPipe implements PipeTransform {
  private readonly _locale = inject(LOCALE_ID);

  transform(value: number | string | null | undefined): string {
    return formatVnd(value, this._locale);
  }
}
