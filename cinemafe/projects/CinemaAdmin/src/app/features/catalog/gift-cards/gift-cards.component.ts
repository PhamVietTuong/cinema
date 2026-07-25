import { Component, inject } from '@angular/core';
import { EMPTY, Observable } from 'rxjs';
import { FormGroup, Validators } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, PaymentServiceAgent, ToastService } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ModalComponent } from '../../../shared/modal.component';

type Dto = PaymentServiceAgent.GiftCardDTO;

@Component({
  selector: 'app-gift-cards',
  standalone: true,
  imports: [SharedModule, ModalComponent],
  templateUrl: './gift-cards.component.html',
})
export class GiftCardsManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(PaymentServiceAgent.HttpService);
  private _toast = inject(ToastService);
  private _translate = inject(TranslateService);

  buildForm(): FormGroup {
    return this._fb.group({
      amount: [null, [Validators.required, Validators.min(1)]],
      expiresAt: [''],
      issuedToEmail: [''],
    });
  }

  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getGiftCards(PaymentServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters }));
  }

  // Gift cards are issued (not edited) and never deleted — those hooks are unused.
  create(): Observable<unknown> { return EMPTY; }
  update(): Observable<unknown> { return EMPTY; }
  remove(): Observable<unknown> { return EMPTY; }

  /** Issue a new gift card, then reload and toast the generated code. */
  issue(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const request = PaymentServiceAgent.IssueGiftCardRequest.fromJS({
      amount: v.amount,
      expiresAt: v.expiresAt || undefined,
      issuedToEmail: v.issuedToEmail?.trim() || undefined,
    });
    this._svc.issueGiftCard(request).subscribe({
      next: card => {
        this._toast.success(this._translate.instant('giftCards.issued', { code: card.code }));
        this.cancelEdit();
        this.load();
      },
      error: e => {
        this._toast.error(this._err(e, this._translate.instant('giftCards.issueFailed')));
      },
    });
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
          this.load();
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
