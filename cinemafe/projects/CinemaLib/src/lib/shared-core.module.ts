import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { SvgIconComponent } from '@ngneat/svg-icon';
import { TranslatePipe, TranslateDirective } from '@ngx-translate/core';
import { LanguageSwitcherComponent } from './i18n/language-switcher.component';

const CORE = [
  CommonModule,
  RouterModule,
  MatButtonModule,
  MatIconModule,
  MatSidenavModule,
  MatMenuModule,
  MatDividerModule,
  MatTooltipModule,
  MatBadgeModule,
  MatDialogModule,
  MatSnackBarModule,
  SvgIconComponent,
  TranslatePipe,
  TranslateDirective,
  LanguageSwitcherComponent,
];

/**
 * The slim subset of {@link SharedModule} that an app's root shell needs (sidenav, menu, icons, translate,
 * language switcher). The root AppModule and the shell import this instead of the all-in-one SharedModule so
 * tables, tabs, paginator, datepicker, form fields and the rest stay in the lazy feature chunks that use them.
 */
@NgModule({
  imports: CORE,
  exports: CORE,
})
export class SharedCoreModule {}
