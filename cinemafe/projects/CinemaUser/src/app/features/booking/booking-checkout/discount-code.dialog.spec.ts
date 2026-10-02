import { of } from 'rxjs';
import { DiscountCodeDialog } from './discount-code.dialog';

describe('DiscountCodeDialog', () => {
  let close: ReturnType<typeof vi.fn>;
  let check: ReturnType<typeof vi.fn>;
  const destroyRef = { onDestroy: () => () => { /* no-op */ } };

  const build = (code = '') => {
    close = vi.fn();
    check = vi.fn().mockImplementation((c: string) =>
      of(c === 'GOOD' ? { valid: true, message: 'GOOD saves 20,000đ', discountAmount: 20000 } : { valid: false, message: 'Invalid', discountAmount: 0 }));
    return new DiscountCodeDialog({ close } as never, { code, check: check as never }, destroyRef as never);
  };

  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('checks the code once typing pauses, not on every keystroke', () => {
    const d = build();

    d.onCodeChange('G');
    d.onCodeChange('GO');
    d.onCodeChange('GOOD');
    vi.advanceTimersByTime(DiscountCodeDialog.CHECK_DELAY_MS + 10);

    expect(check).toHaveBeenCalledTimes(1);
    expect(check).toHaveBeenCalledWith('GOOD');
    expect(d.valid).toBe(true);
    expect(d.discountAmount).toBe(20000);
  });

  it('only lets a verified code be applied', () => {
    const d = build();
    d.code = 'BAD';
    d.onCodeChange('BAD');
    vi.advanceTimersByTime(DiscountCodeDialog.CHECK_DELAY_MS + 10);

    d.apply();

    expect(d.valid).toBe(false);
    expect(close).not.toHaveBeenCalled();
  });

  it('closes with the verified code and its saving', () => {
    const d = build();
    d.code = 'GOOD';
    d.onCodeChange('GOOD');
    vi.advanceTimersByTime(DiscountCodeDialog.CHECK_DELAY_MS + 10);

    d.apply();

    expect(close).toHaveBeenCalledWith({ code: 'GOOD', valid: true, message: 'GOOD saves 20,000đ', discountAmount: 20000 });
  });

  it('clears an earlier verdict as soon as the code is edited', () => {
    const d = build();
    d.code = 'GOOD';
    d.onCodeChange('GOOD');
    vi.advanceTimersByTime(DiscountCodeDialog.CHECK_DELAY_MS + 10);

    d.onCodeChange('GOODX');

    expect(d.valid).toBeNull();
    expect(d.discountAmount).toBe(0);
  });

  it('checks a code that is already entered when the dialog opens', () => {
    const d = build('GOOD');

    d.ngOnInit();

    expect(check).toHaveBeenCalledWith('GOOD');
    expect(d.valid).toBe(true);
  });
});
