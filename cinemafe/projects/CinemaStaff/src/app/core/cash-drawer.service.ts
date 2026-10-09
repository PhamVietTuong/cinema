import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { CinemaServiceAgent } from 'CinemaLib';
import { TheaterContextService } from './theater-context.service';

/**
 * The signed-in cashier's open cash drawer in the current theater, shared by the counter POS (cash needs an open
 * drawer) and the drawer page. Call `refresh()` after the theater changes or a cash sale.
 */
@Injectable({ providedIn: 'root' })
export class CashDrawerService {
  private readonly _svc = inject(CinemaServiceAgent.HttpService);
  private readonly _theater = inject(TheaterContextService);

  /** Latest drawer state; null until the first load. */
  readonly drawer = signal<CinemaServiceAgent.CashDrawerDTO | null>(null);
  readonly isOpen = computed(() => !!this.drawer()?.isOpen);

  private get _theaterId(): string | undefined {
    return this._theater.currentTheaterId() ?? undefined;
  }

  refresh(): Observable<CinemaServiceAgent.CashDrawerDTO> {
    return this._svc
      .getMyDrawer(CinemaServiceAgent.BoxOfficeScopeRequest.fromJS({ theaterId: this._theaterId }))
      .pipe(tap(d => this.drawer.set(d)));
  }

  open(terminalName: string, openingFloat: number): Observable<CinemaServiceAgent.CashDrawerDTO> {
    return this._svc
      .openDrawer(CinemaServiceAgent.OpenDrawerRequest.fromJS({ theaterId: this._theaterId, terminalName, openingFloat }))
      .pipe(tap(d => this.drawer.set(d)));
  }

  payInOut(
    type: CinemaServiceAgent.CashMovementType, amount: number, note: string, override?: CinemaServiceAgent.ManagerOverrideDTO,
  ): Observable<CinemaServiceAgent.CashDrawerDTO> {
    return this._svc
      .payInOut(CinemaServiceAgent.PayInOutRequest.fromJS({ theaterId: this._theaterId, type, amount, note, override }))
      .pipe(tap(d => this.drawer.set(d)));
  }
}
