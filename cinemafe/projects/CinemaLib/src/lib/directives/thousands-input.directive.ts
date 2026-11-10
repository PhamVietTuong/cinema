import { Directive, ElementRef, HostListener, forwardRef } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/** Formats a whole-number text input with "." thousands separators as the user types (1000 → 1.000,
 * the Vietnamese convention). The bound form control holds a plain number (or null when empty):
 * `<input cinemaThousands formControlName="pointsPerUnit">`. Digits only; anything else is dropped. */
@Directive({
  selector: 'input[cinemaThousands]',
  standalone: true,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => ThousandsInputDirective), multi: true }],
  host: { inputmode: 'numeric', autocomplete: 'off' },
})
export class ThousandsInputDirective implements ControlValueAccessor {
  private _onChange: (value: number | null) => void = () => {};
  private _onTouched: () => void = () => {};

  constructor(private _el: ElementRef<HTMLInputElement>) {}

  writeValue(value: number | null | undefined): void {
    this._el.nativeElement.value = this._format(value == null ? '' : String(Math.trunc(Number(value))));
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this._onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this._onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this._el.nativeElement.disabled = disabled;
  }

  @HostListener('input')
  onInput(): void {
    const input = this._el.nativeElement;
    const caret = input.selectionStart ?? input.value.length;
    // Count the digits left of the caret so it can be restored after reformatting.
    const digitsBeforeCaret = input.value.slice(0, caret).replace(/\D/g, '').length;
    const digits = input.value.replace(/\D/g, '').replace(/^0+(?=\d)/, '');

    input.value = this._format(digits);
    let pos = 0;
    let seen = 0;
    while (pos < input.value.length && seen < digitsBeforeCaret) {
      if (/\d/.test(input.value[pos])) {
        seen++;
      }
      pos++;
    }
    input.setSelectionRange(pos, pos);

    this._onChange(digits === '' ? null : Number(digits));
  }

  @HostListener('blur')
  onBlur(): void {
    this._onTouched();
  }

  private _format(digits: string): string {
    return digits.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  }
}
