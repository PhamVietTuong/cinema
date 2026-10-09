import { ScanTone, StaffServiceAgent, scanOutcomeSpec } from 'CinemaLib';

/** One line of the "recent scans" list. */
export interface RecentScan {
  code: string;
  outcome: StaffServiceAgent.ScanOutcome;
  tone: ScanTone;
  seatLabel: string;
  movieTitle: string;
  at: Date;
}

/** How many recent scans the gate screen remembers. */
export const RECENT_SCAN_LIMIT = 10;

/** Scanners and pasted codes often carry stray whitespace or a line break; the API wants the bare code. */
export function normalizeScanCode(raw: string | null | undefined): string {
  return (raw ?? '').replace(/[\r\n\t]+/g, '').trim();
}

/** Inputs of a scan beyond the code itself. */
export interface ScanOptions {
  theaterId?: string | null;
  showTimeId?: string | null;
  ageConfirmed?: boolean;
}

/** Builds the Gate/Scan request; empty optional ids are sent as absent. */
export function buildScanRequest(code: string, options: ScanOptions = {}): StaffServiceAgent.ScanTicketRequest {
  return StaffServiceAgent.ScanTicketRequest.fromJS({
    code: normalizeScanCode(code),
    theaterId: options.theaterId || undefined,
    showTimeId: options.showTimeId || undefined,
    ageConfirmed: options.ageConfirmed === true,
  });
}

/** The ticket is not used yet and waits for the gate keeper to confirm the patron's age. */
export function needsAgePrompt(result: StaffServiceAgent.ScanTicketResultDTO | null | undefined): boolean {
  return result?.outcome === StaffServiceAgent.ScanOutcome.AgeCheckRequired;
}

/** Whether the scan let the patron in. */
export function isAdmitted(result: StaffServiceAgent.ScanTicketResultDTO | null | undefined): boolean {
  return result?.outcome === StaffServiceAgent.ScanOutcome.Admitted;
}

/** Whether the "who/when" of an earlier admission should be shown. */
export function showsUsageDetails(result: StaffServiceAgent.ScanTicketResultDTO | null | undefined): boolean {
  return result?.outcome === StaffServiceAgent.ScanOutcome.AlreadyUsed;
}

/**
 * Request for the second pass after the gate keeper confirmed the age check: the same code and filters,
 * now with `ageConfirmed`. Returns null when the result does not ask for an age check.
 */
export function buildAgeConfirmRequest(
  code: string,
  result: StaffServiceAgent.ScanTicketResultDTO | null | undefined,
  options: ScanOptions = {},
): StaffServiceAgent.ScanTicketRequest | null {
  if (!needsAgePrompt(result)) {
    return null;
  }
  return buildScanRequest(code, { ...options, ageConfirmed: true });
}

/** Newest-first list capped at `limit`; an age-check prompt is not recorded (its confirmation re-scan is). */
export function pushRecentScan(
  recent: readonly RecentScan[],
  code: string,
  result: StaffServiceAgent.ScanTicketResultDTO,
  at: Date,
  limit: number = RECENT_SCAN_LIMIT,
): RecentScan[] {
  if (needsAgePrompt(result)) {
    return [...recent];
  }
  const entry: RecentScan = {
    code,
    outcome: result.outcome ?? StaffServiceAgent.ScanOutcome.NotFound,
    tone: scanOutcomeSpec(result.outcome).tone,
    seatLabel: result.seatLabel ?? '',
    movieTitle: result.movieTitle ?? '',
    at,
  };
  return [entry, ...recent].slice(0, limit);
}
