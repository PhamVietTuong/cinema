import { FormControl, FormGroup } from '@angular/forms';
import { isValidOverridePin, pinsMatchValidator } from './override-pin';

describe('override PIN rules', () => {
  it('accepts 4 to 8 digits only', () => {
    expect(isValidOverridePin('1234')).toBe(true);
    expect(isValidOverridePin('12345678')).toBe(true);
    expect(isValidOverridePin('123')).toBe(false);
    expect(isValidOverridePin('123456789')).toBe(false);
    expect(isValidOverridePin('12a4')).toBe(false);
    expect(isValidOverridePin('')).toBe(false);
    expect(isValidOverridePin(null)).toBe(false);
  });

  it('requires the confirmation to match', () => {
    const group = new FormGroup({ pin: new FormControl('1234'), confirmPin: new FormControl('1234') });
    expect(pinsMatchValidator(group)).toBeNull();
    group.get('confirmPin')?.setValue('4321');
    expect(pinsMatchValidator(group)).toEqual({ pinMismatch: true });
  });
});
