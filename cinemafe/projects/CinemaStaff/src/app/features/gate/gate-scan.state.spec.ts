import { StaffServiceAgent, scanOutcomeSpec, statusPillSpec } from 'CinemaLib';
import {
  RECENT_SCAN_LIMIT,
  buildAgeConfirmRequest,
  buildScanRequest,
  isAdmitted,
  needsAgePrompt,
  normalizeScanCode,
  pushRecentScan,
  showsUsageDetails,
} from './gate-scan.state';

const Outcome = StaffServiceAgent.ScanOutcome;

function result(outcome: StaffServiceAgent.ScanOutcome, extra: Partial<StaffServiceAgent.IScanTicketResultDTO> = {}) {
  return StaffServiceAgent.ScanTicketResultDTO.fromJS({ outcome, seatLabel: 'A1', movieTitle: 'Movie', ...extra });
}

describe('scan outcome tone mapping', () => {
  it('admits in green, refuses in red and asks for judgement in amber', () => {
    expect(scanOutcomeSpec(Outcome.Admitted).tone).toBe('success');
    expect(scanOutcomeSpec(Outcome.NotFound).tone).toBe('danger');
    expect(scanOutcomeSpec(Outcome.NotPaid).tone).toBe('danger');
    expect(scanOutcomeSpec(Outcome.AlreadyUsed).tone).toBe('danger');
    expect(scanOutcomeSpec(Outcome.WrongTheater).tone).toBe('danger');
    expect(scanOutcomeSpec(Outcome.Expired).tone).toBe('danger');
    expect(scanOutcomeSpec(Outcome.WrongShowTime).tone).toBe('warn');
    expect(scanOutcomeSpec(Outcome.TooEarly).tone).toBe('warn');
    expect(scanOutcomeSpec(Outcome.AgeCheckRequired).tone).toBe('warn');
  });

  it('defines a label key for every outcome', () => {
    const outcomes = Object.values(Outcome).filter(v => typeof v === 'number') as number[];
    for (const outcome of outcomes) {
      expect(scanOutcomeSpec(outcome).labelKey).toMatch(/^gate\.outcome\./);
    }
  });

  it('treats an unknown outcome as a refusal', () => {
    expect(scanOutcomeSpec(undefined).tone).toBe('danger');
  });

  it('feeds the shared status pill', () => {
    expect(statusPillSpec('scanOutcome', Outcome.Admitted).cssClass).toBe('ad-pill--success');
    expect(statusPillSpec('scanOutcome', Outcome.TooEarly).cssClass).toBe('ad-pill--warn');
  });
});

describe('scan request building', () => {
  it('trims scanner whitespace and line breaks', () => {
    expect(normalizeScanCode('  QR-123\r\n')).toBe('QR-123');
    expect(normalizeScanCode(null)).toBe('');
  });

  it('omits empty optional ids and defaults ageConfirmed to false', () => {
    const request = buildScanRequest(' QR-1 ', { theaterId: '', showTimeId: null });
    expect(request.code).toBe('QR-1');
    expect(request.theaterId).toBeUndefined();
    expect(request.showTimeId).toBeUndefined();
    expect(request.ageConfirmed).toBe(false);
  });

  it('carries the theater and showtime filter', () => {
    const request = buildScanRequest('QR-1', { theaterId: 't1', showTimeId: 's1' });
    expect(request.theaterId).toBe('t1');
    expect(request.showTimeId).toBe('s1');
  });
});

describe('scan flow state', () => {
  it('prompts for age only on AgeCheckRequired', () => {
    expect(needsAgePrompt(result(Outcome.AgeCheckRequired))).toBe(true);
    expect(needsAgePrompt(result(Outcome.Admitted))).toBe(false);
    expect(needsAgePrompt(null)).toBe(false);
  });

  it('re-scans the same code with ageConfirmed after the prompt', () => {
    const request = buildAgeConfirmRequest('QR-9', result(Outcome.AgeCheckRequired), { theaterId: 't1', showTimeId: 's1' });
    expect(request?.code).toBe('QR-9');
    expect(request?.ageConfirmed).toBe(true);
    expect(request?.theaterId).toBe('t1');
    expect(request?.showTimeId).toBe('s1');
  });

  it('does not build a confirmation re-scan for other outcomes', () => {
    expect(buildAgeConfirmRequest('QR-9', result(Outcome.Admitted))).toBeNull();
  });

  it('flags admitted and already-used results', () => {
    expect(isAdmitted(result(Outcome.Admitted))).toBe(true);
    expect(isAdmitted(result(Outcome.AlreadyUsed))).toBe(false);
    expect(showsUsageDetails(result(Outcome.AlreadyUsed))).toBe(true);
    expect(showsUsageDetails(result(Outcome.Admitted))).toBe(false);
  });

  it('records recent scans newest first and caps the list', () => {
    let recent = pushRecentScan([], 'A', result(Outcome.Admitted), new Date(1));
    recent = pushRecentScan(recent, 'B', result(Outcome.NotFound), new Date(2));
    expect(recent.map(r => r.code)).toEqual(['B', 'A']);
    expect(recent[0].tone).toBe('danger');

    for (let i = 0; i < RECENT_SCAN_LIMIT + 5; i++) {
      recent = pushRecentScan(recent, 'C' + i, result(Outcome.Admitted), new Date(i));
    }
    expect(recent.length).toBe(RECENT_SCAN_LIMIT);
  });

  it('does not record the age prompt itself', () => {
    const recent = pushRecentScan([], 'A', result(Outcome.AgeCheckRequired), new Date());
    expect(recent.length).toBe(0);
  });
});
