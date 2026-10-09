/**
 * Operational / audit timestamps (usedAt, openedAt, paidAt, clockInAt, creationTime, ...) are UTC `DateTime`s that the
 * API serialises WITHOUT a zone suffix, so `new Date(...)` in the generated clients reads them as local time and every
 * screen shows them hours off. These helpers re-read such a value as UTC.
 *
 * Do NOT use them for wall-clock values (showtime start/end, shift start/end, due dates, business dates): those are
 * stored as the cinema's local time and are already correct once parsed (see `toWallClockUtc` in date-range.ts).
 */

const ZONE_SUFFIX = /(Z|[+-]\d{2}:?\d{2})$/i;

/**
 * Re-reads a server UTC timestamp as the correct instant.
 *  - A string with a zone suffix is parsed as is; without one it is taken as UTC.
 *  - A Date was already parsed by the browser as local time (the generated clients drop the missing "Z"), so its
 *    local fields are shifted back into UTC fields.
 * Returns undefined for null / undefined / unparsable input.
 */
export function fromServerUtc(value: Date | string | null | undefined): Date | undefined {
  if (value === null || value === undefined || value === '') {
    return undefined;
  }
  if (typeof value === 'string') {
    const text = value.trim();
    const parsed = new Date(ZONE_SUFFIX.test(text) ? text : `${text}Z`);
    return isNaN(parsed.getTime()) ? undefined : parsed;
  }
  if (isNaN(value.getTime())) {
    return undefined;
  }
  return new Date(Date.UTC(
    value.getFullYear(), value.getMonth(), value.getDate(),
    value.getHours(), value.getMinutes(), value.getSeconds(), value.getMilliseconds()));
}

/** Epoch milliseconds of a server UTC timestamp; NaN when missing. */
export function serverUtcMs(value: Date | string | null | undefined): number {
  return fromServerUtc(value)?.getTime() ?? NaN;
}
