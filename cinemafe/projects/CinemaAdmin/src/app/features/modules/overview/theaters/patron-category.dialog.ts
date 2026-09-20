import { Component, Inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.PatronCategoryDTO;

export interface PatronCategoryDialogData {
  theaterId: string;
  seatTypes: CinemaServiceAgent.SeatTypeDTO[];
  /** All rows sharing this logical category (one per enabled seat kind), or null when creating. */
  categoryRows: Dto[] | null;
}

/** Create/edit form for a theater's patron category (Adult/Student/Senior/Child). A logical
 * category is one row per seat kind it may book (Standard/Double) — disabling a kind here deletes
 * its row entirely, which is the entire eligibility rule (no separate allow-list). Resolves `true`
 * on save, `false` on cancel. */
@Component({
  selector: 'app-patron-category-dialog',
  standalone: false,
  templateUrl: './patron-category.dialog.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class PatronCategoryDialog {
  readonly isEditing: boolean;
  readonly seatTypes: CinemaServiceAgent.SeatTypeDTO[];
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<PatronCategoryDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) private _data: PatronCategoryDialogData,
  ) {
    this.isEditing = !!this._data.categoryRows?.length;
    this.seatTypes = this._data.seatTypes;
    const anchor = this._data.categoryRows?.[0];

    this.form = this._fb.group({
      name: [anchor?.name ?? '', Validators.required],
      description: [anchor?.description ?? ''],
      isActive: [anchor?.isActive ?? true],
      prices: this._fb.array(this.seatTypes.map(st => {
        const row = this._data.categoryRows?.find(r => r.seatTypeId === st.id);
        return this._fb.group({
          seatTypeId: [st.id],
          enabled: [!!row],
          price: [row?.price ?? null, [Validators.min(0)]],
        });
      })),
    });
  }

  get prices(): FormArray {
    return this.form.get('prices') as FormArray;
  }

  save(): void {
    if (this.form.controls['name'].invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const priceRows = this.prices.value as { seatTypeId: string; enabled: boolean; price: number | null }[];
    const enabledRows = priceRows.filter(p => p.enabled);
    if (!enabledRows.length || enabledRows.some(p => p.price == null || p.price < 0)) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.value;
    const prices = enabledRows.map(p => CinemaServiceAgent.PatronCategoryPriceItem.fromJS({ seatTypeId: p.seatTypeId, price: p.price }));
    const anchorId = this._data.categoryRows?.[0]?.id;

    const obs = anchorId
      ? this._svc.updatePatronCategory(CinemaServiceAgent.UpdatePatronCategoryRequest.fromJS({
          id: anchorId, theaterId: this._data.theaterId, name: v.name, description: v.description, isActive: v.isActive, prices,
        }))
      : this._svc.createPatronCategory(CinemaServiceAgent.CreatePatronCategoryRequest.fromJS({
          theaterId: this._data.theaterId, name: v.name, description: v.description, isActive: v.isActive, prices,
        }));

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
