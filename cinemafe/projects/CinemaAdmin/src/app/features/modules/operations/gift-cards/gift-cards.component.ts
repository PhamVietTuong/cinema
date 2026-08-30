import { ChangeDetectorRef, Component } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import {
  PaymentServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
  ToastService,
} from 'CinemaLib';
import { GiftCardDialog } from './gift-card.dialog';

type Dto = PaymentServiceAgent.GiftCardDTO;

@Component({
  selector: 'app-gift-cards',
  standalone: false,
  templateUrl: './gift-cards.component.html',
})
export class GiftCardsManagementComponent extends BaseTableComponent {
  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _svc: PaymentServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _toast: ToastService,
    private _translate: TranslateService,
  ) {
    super(cd, fb, router, store);
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({ keyword: [''] });
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._svc.getGiftCards(PaymentServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  openCreate(): void {
    this._dialog.open(GiftCardDialog, { width: '480px' })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  /** Enable or disable a gift card, then reload the current page. */
  toggleActive(x: Dto): void {
    if (!x.id) {
      return;
    }
    const active = !x.isActive;
    this._svc.setGiftCardActive(PaymentServiceAgent.SetGiftCardActiveRequest.fromJS({ id: x.id, active }))
      .subscribe({
        next: () => {
          this._toast.success(this._translate.instant(active ? 'giftCards.enabled' : 'giftCards.disabled'));
          this.triggerSearch();
        },
        error: e => {
          this._toast.error(this._err(e, this._translate.instant('giftCards.updateFailed')));
        },
      });
  }

  private _err(e: any, fallback: string): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.error || x?.message || fallback);
  }
}
