import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { SharedModule, selectCurrentUser } from 'CinemaLib';

/** Staff profile: account summary. */
@Component({
  selector: 'staff-profile',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent {
  private readonly _store = inject(Store);

  readonly user = this._store.selectSignal(selectCurrentUser);
}
