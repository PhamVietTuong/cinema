import { Injectable, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { Observable, of } from 'rxjs';
import { map, switchMap, take } from 'rxjs/operators';
import { APPROVER_ROLES, DialogService, CinemaServiceAgent, selectCurrentUser } from 'CinemaLib';
import { TheaterContextService } from './theater-context.service';

/** Outcome of asking for manager approval: `override` is absent when the caller is an approver (no PIN needed). */
export interface ApprovalResult {
  override?: CinemaServiceAgent.ManagerOverrideDTO;
}

/**
 * Obtains manager approval for a sensitive counter action. An approver (manager / admin / regional manager)
 * needs none and the request is sent without an override; anyone else picks an approver and types their PIN.
 */
@Injectable({ providedIn: 'root' })
export class ManagerApprovalService {
  private readonly _store = inject(Store);
  private readonly _dialog = inject(DialogService);
  private readonly _theater = inject(TheaterContextService);

  /** Emits the approval, or undefined when the dialog was cancelled. */
  request(): Observable<ApprovalResult | undefined> {
    return this._store.select(selectCurrentUser).pipe(
      take(1),
      switchMap(user => {
        if (APPROVER_ROLES.includes(user?.userTypeName ?? '')) {
          return of<ApprovalResult | undefined>({});
        }
        return this._dialog
          .openManagerOverrideDialog({ theaterId: this._theater.currentTheaterId() ?? undefined })
          .afterClosed()
          .pipe(map(override => (override ? { override } : undefined)));
      }),
    );
  }
}
