import { toDateKey } from 'CinemaLib';

/** Builds the `yyyy-MM-dd` value an `<input type="date">` needs from a `DateTime` DTO field.
 *
 * NSwag parses a backend `DateTime` without a `Z` as local midnight, so the local calendar date is the
 * true one. Going through `toISOString()` converts to UTC first and, whenever the browser is ahead of UTC
 * (Vietnam is UTC+7), shifts the date back a day, so every open-and-save of an edit dialog would silently
 * move the saved date a day earlier. Do not use this for `DateOnly` fields: those parse as UTC midnight. */
export function toDateInput(value: Date | string | null | undefined): string {
  if (!value) {
    return '';
  }
  return toDateKey(new Date(value));
}
