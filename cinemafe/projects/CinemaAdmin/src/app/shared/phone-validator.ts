import { AbstractControl, ValidationErrors } from '@angular/forms';

/** A Vietnamese phone number (`+84` or a leading `0`, then 9 or 10 digits) that may be written with the separators
 * people type: spaces, dots, dashes and brackets (`0274-3822-515`, `(028) 3822 5151`). An empty value is allowed.
 *
 * Fails with the `pattern` error key, so a template that already shows its message for the plain
 * `Validators.pattern` keeps working. Used for theater phone numbers, which are landlines written with separators
 * in the seed data: the strict digits-only pattern refused to save those theaters. User phone numbers stay strict
 * because they are used to sign in. */
export function phoneWithSeparators(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '').trim();
  if (!value) {
    return null;
  }
  const digits = value.replace(/[\s().-]/g, '');
  return /^(?:\+84|0)\d{9,10}$/.test(digits) ? null : { pattern: { requiredPattern: 'phone', actualValue: value } };
}
