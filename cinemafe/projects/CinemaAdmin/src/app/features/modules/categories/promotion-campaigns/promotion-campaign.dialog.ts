import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.PromotionCampaignDTO;

export interface PromotionCampaignDialogData {
  campaign: Dto | null;
}

/** Create/edit form for a promotion campaign (e.g. "C'Student", "Happy Hour"), opened via MatDialog.
 * Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-promotion-campaign-dialog',
  standalone: false,
  templateUrl: './promotion-campaign.dialog.html',
})
export class PromotionCampaignDialog {
  readonly editingId: string | null;
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<PromotionCampaignDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: PromotionCampaignDialogData,
  ) {
    this.editingId = data.campaign?.id ?? null;
    this.form = this._fb.group({
      name: [data.campaign?.name ?? '', Validators.required],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updatePromotionCampaign(CinemaServiceAgent.UpdatePromotionCampaignRequest.fromJS({ ...v, id: this.editingId }))
      : this._svc.createPromotionCampaign(CinemaServiceAgent.CreatePromotionCampaignRequest.fromJS(v));

    this._store.dispatch(showLoading());
    obs.subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}
