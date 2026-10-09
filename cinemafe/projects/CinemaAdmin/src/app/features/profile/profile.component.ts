import { Component } from '@angular/core';
import { SharedModule, ProfileFormBase } from 'CinemaLib';

@Component({
  selector: 'app-admin-profile',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent extends ProfileFormBase {
  tab: 'info' | 'password' = 'info';

  protected override _profileI18n(): { updateSuccess: string; updateFailed: string } {
    return { updateSuccess: 'profile.profileUpdateSuccess', updateFailed: 'profile.profileUpdateFailed' };
  }

  protected override _initialsFallback(): string {
    return 'QT';
  }
}
