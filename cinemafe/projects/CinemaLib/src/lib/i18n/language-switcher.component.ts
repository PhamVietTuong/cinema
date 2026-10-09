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
  templateUrl: './language-switcher.component.html',
  styleUrl: './language-switcher.component.scss',
})
export class LanguageSwitcherComponent {
  readonly lang = inject(LanguageService);

  select(code: AppLanguage): void {
    this.lang.use(code);
  }
}
