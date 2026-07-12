import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatIconModule } from '@angular/material/icon';
import { AppLanguage, LanguageService } from './language.service';

/**
 * Compact language picker (VI/EN) for the app toolbars. Shows the active
 * locale and opens a menu to switch. Language names are always rendered in
 * their own language, so no translation keys are needed here.
 */
@Component({
  selector: 'cl-language-switcher',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatMenuModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      mat-button
      [matMenuTriggerFor]="langMenu"
      class="cl-language-switcher"
      aria-label="Language">
      <mat-icon>language</mat-icon>
      <span class="cl-language-switcher__code">{{ lang.current() | uppercase }}</span>
    </button>
    <mat-menu #langMenu="matMenu">
      @for (option of lang.languages; track option.code) {
        <button mat-menu-item (click)="select(option.code)">
          @if (option.code === lang.current()) {
            <mat-icon>check</mat-icon>
          } @else {
            <mat-icon>&nbsp;</mat-icon>
          }
          <span>{{ option.label }}</span>
        </button>
      }
    </mat-menu>
  `,
  styles: [
    `
      .cl-language-switcher__code {
        margin-left: 4px;
        font-weight: 600;
        letter-spacing: 0.5px;
      }
    `,
  ],
})
export class LanguageSwitcherComponent {
  readonly lang = inject(LanguageService);

  select(code: AppLanguage): void {
    this.lang.use(code);
  }
}
