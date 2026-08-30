import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { PaymentServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

/** Issue-gift-card form, opened via MatDialog. Resolves `true` on issue, `false` on cancel. */
@Component({
  selector: 'app-gift-card-dialog',
  standalone: false,
  templateUrl: './gift-card.dialog.html',
})
export class GiftCardDialog {
  form: FormGroup;

  constructor(
    private _svc: PaymentServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<GiftCardDialog, boolean>,
    private _translate: TranslateService,
  ) {
    this.form = this._fb.group({
      amount: [null, [Validators.required, Validators.min(1)]],
      expiresAt: [''],
      issuedToEmail: [''],
    });
  }

  save(): void {
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

    this._store.dispatch(showLoading());
    this._svc.issueGiftCard(request).subscribe({
      next: card => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('giftCards.issued', { code: card.code }) }));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}
