import { Injectable, inject } from '@angular/core';
import { EMPTY, Observable, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { DialogService, StaffServiceAgent, apiErrorMessage } from 'CinemaLib';

/** Server answer for a sensitive action sent without (or with a wrong) manager override. */
const OVERRIDE_REJECTION = /approval/i;

/** True when the API refused the call (403) because the manager approval was missing or wrong, so the PIN prompt should reopen. */
export function isOverrideRejection(error: unknown): boolean {
  const status = (error as { status?: number } | null | undefined)?.status;
  return status === 403 && OVERRIDE_REJECTION.test(apiErrorMessage(error, ''));
}

/**
 * Runs a sensitive API call. `askPin` opens the manager PIN dialog first; otherwise the call goes without an override and a 403
 * about the approval reopens the dialog and retries with the PIN. Cancelling the dialog completes without a value.
 */
@Injectable({ providedIn: 'root' })
export class SensitiveCallService {
  private readonly _dialogs = inject(DialogService);

  run<T>(
    theaterId: string | undefined,
    askPin: boolean,
    call: (override?: StaffServiceAgent.ManagerOverrideDTO) => Observable<T>,
  ): Observable<T> {
    const prompt = (): Observable<T> => this._dialogs.openManagerOverrideDialog({ theaterId }).afterClosed().pipe(
      switchMap(override => (override ? attempt(override) : EMPTY)),
    );
    const attempt = (override?: StaffServiceAgent.ManagerOverrideDTO): Observable<T> => call(override).pipe(
      catchError(error => (isOverrideRejection(error) ? prompt() : throwError(() => error))),
    );
    return askPin ? prompt() : attempt();
  }
}
