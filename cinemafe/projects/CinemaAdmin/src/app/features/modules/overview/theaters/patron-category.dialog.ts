import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.PatronCategoryDTO;

export interface PatronCategoryDialogData {
  theaterId: string;
  seatTypes: CinemaServiceAgent.SeatTypeDTO[];
  /** The row being edited, or null when creating. */
  category: Dto | null;
}

/** Create/edit form for a single, independent PatronCategory pricing row (e.g. "Adult" / Standard).
 * Rows are never grouped or merged by Name — editing or deleting one row never affects any other
 * row, even one sharing the same Name. Resolves `true` on save, `false` on cancel. */
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
    this.isEditing = !!this._data.category;
    this.seatTypes = this._data.seatTypes;
    const category = this._data.category;

    this.form = this._fb.group({
      name: [category?.name ?? '', Validators.required],
      description: [category?.description ?? ''],
      isActive: [category?.isActive ?? true],
      seatTypeId: [category?.seatTypeId ?? this.seatTypes[0]?.id, Validators.required],
      price: [category?.price ?? null, [Validators.required, Validators.min(0)]],
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.value;
    const category = this._data.category;

    const obs = category
      ? this._svc.updatePatronCategory(CinemaServiceAgent.UpdatePatronCategoryRequest.fromJS({
          id: category.id, name: v.name, description: v.description, isActive: v.isActive, seatTypeId: v.seatTypeId, price: v.price,
        }))
      : this._svc.createPatronCategory(CinemaServiceAgent.CreatePatronCategoryRequest.fromJS({
          theaterId: this._data.theaterId, name: v.name, description: v.description, isActive: v.isActive, seatTypeId: v.seatTypeId, price: v.price,
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
