import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { APPROVER_ROLES, SharedModule, selectCurrentUser } from 'CinemaLib';
import { DailyCloseComponent } from './daily-close.component';
import { MyDrawerComponent } from './my-drawer.component';

/** Cash close page: my drawer tab for every seller, daily close tab for approvers only. */
@Component({
  selector: 'staff-cash-close-page',
  standalone: true,
  imports: [SharedModule, MyDrawerComponent, DailyCloseComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cash-close-page.component.html',
  styleUrl: './cash-close-page.component.scss',
})
export class CashClosePageComponent {
  private readonly _user = inject(Store).selectSignal(selectCurrentUser);

  isApprover(): boolean {
    return APPROVER_ROLES.includes(this._user()?.userTypeName ?? '');
  }
}
