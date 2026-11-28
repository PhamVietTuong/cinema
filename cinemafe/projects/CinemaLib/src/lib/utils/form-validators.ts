import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Vietnamese phone number: `0` or `+84` followed by 9–10 digits. Same rule the API enforces. */
export const VN_PHONE_PATTERN = /^(?:\+84|0)\d{9,10}$/;

/**
 * Group validator: the confirmation field must equal the password field. Sets `passwordMismatch` only when both are
 * filled in, so an empty field is left to `Validators.required`.
 */
export function passwordsMatch(passwordField = 'password', confirmField = 'confirmPassword'): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const password = group.get(passwordField)?.value;
    const confirm = group.get(confirmField)?.value;
    return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
  };
}
