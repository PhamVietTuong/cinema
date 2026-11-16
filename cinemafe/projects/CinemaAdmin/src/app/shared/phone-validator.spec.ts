import { FormControl } from '@angular/forms';
import { phoneWithSeparators } from './phone-validator';

describe('phoneWithSeparators', () => {
  const check = (value: string | null) => phoneWithSeparators(new FormControl(value));

  it('accepts an empty value', () => {
    expect(check('')).toBeNull();
    expect(check(null)).toBeNull();
    expect(check('   ')).toBeNull();
  });

  it('accepts plain digits and the +84 form', () => {
    expect(check('0912345678')).toBeNull();
    expect(check('02838225151')).toBeNull();
    expect(check('+84912345678')).toBeNull();
  });

  it('accepts the separators people type', () => {
    expect(check('0274-3822-515')).toBeNull();
    expect(check('(028) 3822 5151')).toBeNull();
    expect(check('0912.345.678')).toBeNull();
  });

  it('rejects numbers that are not Vietnamese phone numbers', () => {
    expect(check('12345')).toEqual({ pattern: expect.anything() });
    expect(check('0912-345')).toEqual({ pattern: expect.anything() });
    expect(check('abc0912345678')).toEqual({ pattern: expect.anything() });
    expect(check('84912345678')).toEqual({ pattern: expect.anything() });
  });
});
