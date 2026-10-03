import { ChangeDetectorRef, Component, Inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { Observable, of } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';
import { ImageUploadService } from '../../../../shared/image-upload.service';

type Dto = CinemaServiceAgent.FoodAndDrinkDTO;

export interface FoodAndDrinkDialogData {
  theaterId: string;
  foodAndDrink: Dto | null;
  /** Create-mode only: start with the combo toggle ON and locked (opened from the Combo tab). */
  presetCombo?: boolean;
}

/** Create/edit form for a theater's food & drink item, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-food-and-drink-dialog',
  standalone: false,
  templateUrl: './food-and-drink.dialog.html',
})
export class FoodAndDrinkDialog {
  editingId: string | null;
  /** Fixed when the dialog opens, so the title does not flip to 'edit' after a first save creates the item. */
  readonly isEditMode: boolean;
  form: FormGroup;

  /** This theater's non-combo foods, selectable as combo components. */
  componentOptions: Dto[] = [];
  /** Tracked items cannot become combos. */
  readonly isTracked: boolean;
  comboRowsError = false;

  uploading = false;
  uploadError = '';

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _cdr: ChangeDetectorRef,
    private _upload: ImageUploadService,
    private _dialogRef: MatDialogRef<FoodAndDrinkDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) private _data: FoodAndDrinkDialogData,
  ) {
    this.editingId = _data.foodAndDrink?.id ?? null;
    this.isEditMode = !!_data.foodAndDrink?.id;
    this.form = this._fb.group({
      name: [_data.foodAndDrink?.name ?? '', Validators.required],
      price: [_data.foodAndDrink?.price ?? 0, [Validators.required, Validators.min(0)]],
      imageUrl: [_data.foodAndDrink?.imageUrl ?? ''],
      description: [_data.foodAndDrink?.description ?? ''],
      isAvailable: [_data.foodAndDrink?.isAvailable ?? true],
      isCombo: [{
        value: _data.presetCombo ? true : (_data.foodAndDrink?.isCombo ?? false),
        disabled: !!_data.presetCombo || !!_data.foodAndDrink?.trackInventory,
      }],
      components: this._fb.array([]),
    });
    this.isTracked = !!_data.foodAndDrink?.trackInventory;
    if (_data.presetCombo) {
      this.addRow();
    }
    this._loadComponentOptions();
    if (_data.foodAndDrink?.isCombo && this.editingId) {
      this._loadRecipe(this.editingId);
    }
  }

  get rows(): FormArray {
    return this.form.get('components') as FormArray;
  }

  get isCombo(): boolean {
    return !!this.form.get('isCombo')?.value;
  }

  addRow(componentId = '', quantity = 1): void {
    this.rows.push(this._fb.group({
      componentId: [componentId, Validators.required],
      quantity: [quantity, [Validators.required, Validators.min(1), Validators.max(100)]],
    }));
    this.comboRowsError = false;
  }

  removeRow(index: number): void {
    this.rows.removeAt(index);
  }

  onComboToggle(): void {
    if (this.isCombo && this.rows.length === 0) {
      this.addRow();
    }
    this.comboRowsError = false;
  }

  /** Options for one row: excludes components already chosen in other rows. */
  optionsFor(index: number): Dto[] {
    const chosen = new Set<string>();
    this.rows.controls.forEach((c, i) => {
      if (i !== index && c.value.componentId) {
        chosen.add(c.value.componentId);
      }
    });
    return this.componentOptions.filter(o => !chosen.has(o.id!));
  }

  private _loadComponentOptions(): void {
    this._svc.getFoodAndDrinks(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 200, filters: { theaterId: this._data.theaterId },
    })).subscribe({
      next: res => {
        this.componentOptions = (res.results ?? []).filter(f => !f.isCombo && f.id !== this.editingId);
        this._cdr.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    });
  }

  private _loadRecipe(comboId: string): void {
    this._svc.getComboComponents(comboId).subscribe({
      next: items => {
        (items ?? []).forEach(i => this.addRow(i.componentId, i.quantity));
        this._cdr.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    });
  }

  onPickImage(event: Event, controlName: string): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) {
      return;
    }
    this.uploading = true;
    this.uploadError = '';
    this._upload.upload(file).subscribe({
      next: url => {
        this.form.patchValue({ [controlName]: url });
        this.uploading = false;
        this._cdr.markForCheck();
      },
      error: () => {
        this.uploadError = 'Tải ảnh thất bại.';
        this.uploading = false;
        this._cdr.markForCheck();
      },
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    if (this.isCombo && this.rows.length === 0) {
      this.comboRowsError = true;
      return;
    }
    const { name, price, imageUrl, description, isAvailable } = this.form.value;
    const v = { name, price, imageUrl, description, isAvailable };
    const wasCombo = !!this._data.foodAndDrink?.isCombo;
    const isCombo = this.isCombo;

    this._store.dispatch(showLoading());
    const saved$: Observable<string> = this.editingId
      ? this._svc.updateFoodAndDrink(CinemaServiceAgent.UpdateFoodAndDrinkRequest.fromJS({ ...v, id: this.editingId, theaterId: this._data.theaterId }))
        .pipe(map(() => this.editingId as string))
      : this._svc.createFoodAndDrink(CinemaServiceAgent.CreateFoodAndDrinkRequest.fromJS({ ...v, theaterId: this._data.theaterId }))
        .pipe(map(created => {
          // Remember the new id so a retry after a failed composition save updates instead of duplicating.
          this.editingId = created.id ?? null;
          return created.id as string;
        }));

    saved$.pipe(
      switchMap(id => {
        if (!isCombo && !wasCombo) {
          return of(null);
        }
        const components = isCombo
          ? this.rows.controls.map(c => CinemaServiceAgent.ComboComponentItem.fromJS({ componentId: c.value.componentId, quantity: Number(c.value.quantity) }))
          : [];
        return this._svc.saveComboComposition(CinemaServiceAgent.SaveComboRequest.fromJS({ comboId: id, isCombo, components }));
      }),
    ).subscribe({
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
