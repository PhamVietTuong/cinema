import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.DiscountDTO;

export interface DiscountDialogData {
  discount: Dto | null;
}

/** Create/edit form for a discount, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-discount-dialog',
  standalone: false,
  templateUrl: './discount.dialog.html',
  styles: [`
    .ad-hint { display: block; margin-top: 4px; font-size: 12px; color: var(--ad-muted); font-weight: 400; }
    .scope-checklist { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 6px 14px;
      margin-top: 8px; max-height: 180px; overflow-y: auto; padding: 8px; border: 1px solid var(--ad-border); border-radius: 6px; }
    .scope-days { display: flex; flex-wrap: wrap; gap: 6px 16px; margin-top: 8px; }
  `],
})
export class DiscountDialog implements OnInit {
  readonly editingId: string | null;
  form: FormGroup;

  discountTypes: CinemaServiceAgent.DiscountTypeDTO[] = [];
  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  movies: CinemaServiceAgent.MovieDTO[] = [];

  /** Theaters ticked when the promotion is limited (ApplyToAllTheaters = false). */
  selectedTheaterIds = new Set<string>();
  /** Weekday numbers ticked (0 = Sunday … 6 = Saturday), matching the backend bitmask. */
  selectedDays = new Set<number>();
  readonly weekDays = [
    { value: 0, key: 'sun' }, { value: 1, key: 'mon' }, { value: 2, key: 'tue' },
    { value: 3, key: 'wed' }, { value: 4, key: 'thu' }, { value: 5, key: 'fri' },
    { value: 6, key: 'sat' },
  ];

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _cdr: ChangeDetectorRef,
    private _dialogRef: MatDialogRef<DiscountDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: DiscountDialogData,
  ) {
    this.editingId = data.discount?.id ?? null;
    const item = data.discount;

    this.selectedTheaterIds = new Set(item?.theaterIds ?? []);
    this.selectedDays = new Set(this._maskToDays(item?.daysOfWeekMask));

    this.form = this._fb.group({
      code: [item?.code ?? ''],
      description: [item?.description ?? ''],
      percent: [item?.percent ?? 0, [Validators.required, Validators.min(0)]],
      maxDiscountAmount: [item?.maxDiscountAmount ?? null],
      discountTypeId: [item?.discountTypeId ?? '', Validators.required],
      startDate: [item?.startDate ? new Date(item.startDate).toISOString().split('T')[0] : '', Validators.required],
      endDate: [item?.endDate ? new Date(item.endDate).toISOString().split('T')[0] : '', Validators.required],
      maxUsage: [item?.maxUsage ?? null],
      isActive: [item?.isActive ?? true],
      // ── Promotion scope ──
      autoApply: [item?.autoApply ?? false],
      applyToAllTheaters: [item?.applyToAllTheaters ?? true],
      movieId: [item?.movieId ?? null],
      startTimeOfDay: [item?.startTimeOfDay ?? ''],
      endTimeOfDay: [item?.endTimeOfDay ?? ''],
    });
    this._syncCodeValidator();
  }

  ngOnInit(): void {
    this._svc.getDiscountTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.discountTypes = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.theaters = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.movies = r.results ?? []; this._cdr.markForCheck(); });
  }

  // ── Scope UI helpers ──────────────────────────────────────────────────────────
  toggleTheater(id?: string): void {
    if (!id) {
      return;
    }
    if (this.selectedTheaterIds.has(id)) {
      this.selectedTheaterIds.delete(id);
    } else {
      this.selectedTheaterIds.add(id);
    }
    this._cdr.markForCheck();
  }

  isTheaterSelected(id?: string): boolean {
    return !!id && this.selectedTheaterIds.has(id);
  }

  toggleDay(value: number): void {
    if (this.selectedDays.has(value)) {
      this.selectedDays.delete(value);
    } else {
      this.selectedDays.add(value);
    }
    this._cdr.markForCheck();
  }

  isDaySelected(value: number): boolean {
    return this.selectedDays.has(value);
  }

  /** Re-evaluates the code field: required for code-based, optional for auto-apply. */
  onAutoApplyChange(): void {
    this._syncCodeValidator();
    this._cdr.markForCheck();
  }

  /** Zoneless: re-render so the theater checklist shows/hides with the toggle. */
  onApplyToAllChange(): void {
    this._cdr.markForCheck();
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateDiscount(CinemaServiceAgent.UpdateDiscountRequest.fromJS({ ...this._payload(v), id: this.editingId }))
      : this._svc.createDiscount(CinemaServiceAgent.CreateDiscountRequest.fromJS(this._payload(v)));

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

  // ── Internals ───────────────────────────────────────────────────────────────
  private _payload(v: any) {
    const applyToAll = !!v.applyToAllTheaters;
    const days = [...this.selectedDays];
    return {
      ...v,
      code: v.code?.trim() || null,
      applyToAllTheaters: applyToAll,
      theaterIds: applyToAll ? [] : [...this.selectedTheaterIds],
      movieId: v.movieId || null,
      daysOfWeekMask: days.length ? days.reduce((mask, d) => mask | (1 << d), 0) : null,
      startTimeOfDay: v.startTimeOfDay || null,
      endTimeOfDay: v.endTimeOfDay || null,
    };
  }

  private _maskToDays(mask?: number): number[] {
    if (!mask) {
      return [];
    }
    return this.weekDays.map(d => d.value).filter(d => (mask & (1 << d)) !== 0);
  }

  private _syncCodeValidator(): void {
    const code = this.form.get('code')!;
    code.setValidators(this.form.value.autoApply ? [] : [Validators.required]);
    code.updateValueAndValidity({ emitEvent: false });
  }
}
