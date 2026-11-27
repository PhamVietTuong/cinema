import { ChangeDetectionStrategy, Component, ElementRef, forwardRef, Input, ViewChild } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

interface ToolbarCommand {
  cmd: string;
  icon: string;
  labelKey: string;
}

/**
 * Minimal rich-text editor built on a `contenteditable` element (bold / italic / underline / lists).
 * Works as a form control: the value is an HTML string; an empty editor yields ''.
 * Pasted content is inserted as plain text so foreign markup never enters the value.
 */
@Component({
  selector: 'app-rich-text-editor',
  standalone: true,
  imports: [CommonModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => RichTextEditorComponent), multi: true }],
  template: `
    <div class="rte" [class.rte--disabled]="disabled">
      <div class="rte__toolbar" role="toolbar">
        @for (c of commands; track c.cmd) {
          <button type="button" class="rte__btn" [disabled]="disabled"
                  [attr.aria-label]="c.labelKey | translate" [title]="c.labelKey | translate"
                  (mousedown)="$event.preventDefault()" (click)="exec(c.cmd)">
            <mat-icon aria-hidden="true">{{ c.icon }}</mat-icon>
          </button>
        }
      </div>
      <div #editor class="rte__area" contenteditable="true" role="textbox" aria-multiline="true"
           [attr.id]="inputId" [attr.contenteditable]="disabled ? 'false' : 'true'"
           [attr.data-placeholder]="placeholder"
           (input)="onInput()" (blur)="onBlur()" (paste)="onPaste($event)"></div>
    </div>
  `,
  styleUrl: './rich-text-editor.component.scss',
})
export class RichTextEditorComponent implements ControlValueAccessor {
  @ViewChild('editor', { static: true }) private _editor!: ElementRef<HTMLElement>;

  @Input() inputId: string | null = null;
  @Input() placeholder = '';
  disabled = false;

  readonly commands: ToolbarCommand[] = [
    { cmd: 'bold', icon: 'format_bold', labelKey: 'richText.bold' },
    { cmd: 'italic', icon: 'format_italic', labelKey: 'richText.italic' },
    { cmd: 'underline', icon: 'format_underlined', labelKey: 'richText.underline' },
    { cmd: 'insertUnorderedList', icon: 'format_list_bulleted', labelKey: 'richText.bulletList' },
    { cmd: 'insertOrderedList', icon: 'format_list_numbered', labelKey: 'richText.numberedList' },
  ];

  private _onChange: (v: string) => void = () => {};
  private _onTouched: () => void = () => {};

  exec(cmd: string): void {
    this._editor.nativeElement.focus();
    document.execCommand(cmd, false);
    this.onInput();
  }

  onInput(): void {
    const el = this._editor.nativeElement;
    // A cleared editor leaves behind "<br>" / empty blocks — normalise to '' so `required` works.
    const empty = !(el.textContent ?? '').trim() && !el.querySelector('li');
    this._onChange(empty ? '' : el.innerHTML);
  }

  onBlur(): void {
    this._onTouched();
  }

  onPaste(e: ClipboardEvent): void {
    e.preventDefault();
    const text = e.clipboardData?.getData('text/plain') ?? '';
    document.execCommand('insertText', false, text);
  }

  writeValue(value: string | null): void {
    this._editor.nativeElement.innerHTML = value ?? '';
  }

  registerOnChange(fn: (v: string) => void): void {
    this._onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this._onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled = disabled;
  }
}
