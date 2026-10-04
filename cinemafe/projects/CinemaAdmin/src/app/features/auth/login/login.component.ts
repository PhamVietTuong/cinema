import { Component } from '@angular/core';
import { LoginFormComponent, LoginStat, ShellBrand } from 'CinemaLib';

/** Admin sign-in: the shared CinemaLib login layout with the admin copy. */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [LoginFormComponent],
  template: `
    <cl-login-form
      [brand]="brand"
      titleKey="login.title"
      subtitleKey="login.subtitle"
      heroTitleLine1Key="login.heroTitleLine1"
      heroTitleLine2Key="login.heroTitleLine2"
      heroSubtitleKey="login.heroSubtitle"
      [stats]="stats"
      footerKey="login.footer" />
  `,
})
export class LoginComponent {
  readonly brand: ShellBrand = { name: 'CINEMA', strong: 'ADMIN' };
  readonly stats: LoginStat[] = [
    { icon: 'movie', labelKey: 'login.statMovies' },
    { icon: 'theaters', labelKey: 'login.statTheaters' },
    { icon: 'confirmation_number', labelKey: 'login.statTickets' },
  ];
}
