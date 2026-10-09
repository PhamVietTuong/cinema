/** Cash-drawer arithmetic shared by the close form and its tests. Amounts are whole dong. */

/** Counted minus expected: negative is short, positive is over. Null while nothing valid is typed. */
export function drawerVariance(expectedCash: number | null | undefined, countedCash: number | null | undefined): number | null {
  if (countedCash === null || countedCash === undefined || Number.isNaN(countedCash)) {
    return null;
  }
  return countedCash - (expectedCash ?? 0);
}

export type VarianceTone = 'balanced' | 'short' | 'over';

export function varianceTone(variance: number | null | undefined): VarianceTone {
  if (!variance) {
    return 'balanced';
  }
  return variance < 0 ? 'short' : 'over';
}

/** A counted amount is valid when it is a finite number, not negative. */
export function isValidCount(countedCash: number | null | undefined): boolean {
  return typeof countedCash === 'number' && Number.isFinite(countedCash) && countedCash >= 0;
}

/**
 * Whether a variance will hold the drawer in "Closed" until a manager reconciles it. The server decides with its configured
 * tolerance; this mirrors it (default 0) only to preview the outcome before the click.
 */
export function needsReconciliation(variance: number | null | undefined, tolerance = 0): boolean {
  return variance !== null && variance !== undefined && Math.abs(variance) > tolerance;
}

/** Local calendar date as yyyy-MM-dd, the value of an `<input type="date">`. */
export function toDateInputValue(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/**
 * Parses an `<input type="date">` value into a Date; undefined when blank or malformed. It lands at local NOON, not midnight:
 * the client serialises dates with toISOString() and the API keeps only the date part, so local midnight east of UTC would
 * round-trip to the previous day.
 */
export function fromDateInputValue(value: string | null | undefined): Date | undefined {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value ?? '');
  if (!match) {
    return undefined;
  }
  return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]), 12);
}
