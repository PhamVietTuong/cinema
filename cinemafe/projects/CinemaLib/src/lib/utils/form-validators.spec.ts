import { FormControl, FormGroup } from '@angular/forms';
import { VN_PHONE_PATTERN, passwordsMatch } from './form-validators';

describe('VN_PHONE_PATTERN', () => {
  it.each(['0912345678', '09123456789', '+84912345678'])('accepts %s', value => {
    expect(VN_PHONE_PATTERN.test(value)).toBe(true);
  });

  it.each(['', '912345678', '091234567', '091234567890', '+8491234', '09123abc78'])('rejects "%s"', value => {
    expect(VN_PHONE_PATTERN.test(value)).toBe(false);
  });
});

describe('passwordsMatch', () => {
  const group = (a: string, b: string, names: [string, string] = ['password', 'confirmPassword']) =>
    new FormGroup({ [names[0]]: new FormControl(a), [names[1]]: new FormControl(b) });

  it('flags a mismatch', () => {
    expect(passwordsMatch()(group('secret1', 'secret2'))).toEqual({ passwordMismatch: true });
  });

  it('passes when equal', () => {
    expect(passwordsMatch()(group('secret1', 'secret1'))).toBeNull();
  });

  it('ignores an empty field (required handles that)', () => {
    expect(passwordsMatch()(group('secret1', ''))).toBeNull();
    expect(passwordsMatch()(group('', 'secret1'))).toBeNull();
  });

  it('supports custom field names', () => {
    expect(passwordsMatch('newPassword')(group('a1', 'b2', ['newPassword', 'confirmPassword']))).toEqual({ passwordMismatch: true });
  });
});
