import { AbstractControl, ValidationErrors } from '@angular/forms';

/** An override PIN is 4 to 8 digits (same rule as the API). */
export const OVERRIDE_PIN_PATTERN = /^\d{4,8}$/;

export function isValidOverridePin(pin: string | null | undefined): boolean {
  return OVERRIDE_PIN_PATTERN.test(pin ?? '');
}

/** Group validator: `pin` and `confirmPin` must be equal. */
export function pinsMatchValidator(group: AbstractControl): ValidationErrors | null {
  const pin = group.get('pin')?.value;
  const confirm = group.get('confirmPin')?.value;
  return pin === confirm ? null : { pinMismatch: true };
}
