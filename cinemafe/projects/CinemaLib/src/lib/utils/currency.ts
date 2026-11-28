import { formatNumber } from '@angular/common';

/** The Vietnamese dong has no minor unit: whole numbers, thousands separated by the locale, suffixed with `đ`. */
export function formatVnd(value: number | string | null | undefined, locale = 'en-US'): string {
  if (value === null || value === undefined || value === '' || Number.isNaN(Number(value))) {
    return '';
  }
  return `${formatNumber(Number(value), locale, '1.0-0')}đ`;
}
