import { Injectable, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { EMPTY, Observable } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { APPROVER_ROLES, DialogService, CinemaServiceAgent, selectCurrentUser } from 'CinemaLib';

/** True when the role may perform an approver-only action directly (no PIN override needed). */
export function isApproverRole(role: string | null | undefined): boolean {
  return APPROVER_ROLES.includes(role ?? '');
}

/**
 * Runs a sensitive API call with the manager-override flow: an approver acts directly, anyone else first
 * gets the shared PIN dialog and the resulting override is passed to `call`. Cancelling the dialog completes
 * without calling the API.
 */
@Injectable({ providedIn: 'root' })
export class OverrideFlowService {
  private readonly _dialogs = inject(DialogService);
  private readonly _user = inject(Store).selectSignal(selectCurrentUser);

  isApprover(): boolean {
    return isApproverRole(this._user()?.userTypeName);
  }

  run<T>(theaterId: string | undefined, call: (override?: CinemaServiceAgent.ManagerOverrideDTO) => Observable<T>): Observable<T> {
    if (this.isApprover()) {
      return call(undefined);
    }
    return this._dialogs.openManagerOverrideDialog({ theaterId }).afterClosed().pipe(
      switchMap(override => {
        if (!override) {
          return EMPTY;
        }
        return call(override);
      }),
    );
  }
}
