import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';
import { CinemaServiceAgent, ToastService, apiErrorMessage } from 'CinemaLib';

type Dto = CinemaServiceAgent.DiscountDTO;

@Component({
  selector: 'app-discounts',
  standalone: false,
  templateUrl: './discounts.component.html',
  styles: [`
    .ad-hint { display: block; margin-top: 4px; font-size: 12px; color: var(--ad-muted); font-weight: 400; }
    .scope-checklist { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 6px 14px;
      margin-top: 8px; max-height: 180px; overflow-y: auto; padding: 8px; border: 1px solid var(--ad-border); border-radius: 6px; }
    .scope-days { display: flex; flex-wrap: wrap; gap: 6px 16px; margin-top: 8px; }
  `],
})
export class DiscountsManagementComponent implements OnInit, OnDestroy {
  items: Dto[] = [];
  totalCount = 0;
  pageIndex = 1;
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 20, 50];
  filters: Record<string, string> = {};

  showForm = false;
  editingId: string | null = null;
  form: FormGroup;
  private readonly _formDefaults: unknown;

  confirmOpen = false;
  private _pendingDeleteId: string | null = null;

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

  private readonly _filter$ = new Subject<void>();
  private readonly _destroy$ = new Subject<void>();

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
    private _toast: ToastService,
    private _translate: TranslateService,
  ) {
    this.form = this._fb.group({
      code: [''],
      description: [''],
      percent: [0, [Validators.required, Validators.min(0)]],
      maxDiscountAmount: [null],
      discountTypeId: ['', Validators.required],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      maxUsage: [null],
      isActive: [true],
      // ── Promotion scope ──
      autoApply: [false],
      applyToAllTheaters: [true],
      movieId: [null],
      startTimeOfDay: [''],
      endTimeOfDay: [''],
    });
    this._formDefaults = this.form.getRawValue();
  }

  ngOnInit(): void {
    this._filter$.pipe(debounceTime(300), takeUntil(this._destroy$)).subscribe(() => {
      this.pageIndex = 1;
      this.load();
    });
    this.load();

    this._svc.getDiscountTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.discountTypes = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.theaters = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.movies = r.results ?? []; this._cdr.markForCheck(); });
  }

  ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
  }

  load(): void {
    this._svc.getDiscounts(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: this.pageIndex, pageSize: this.pageSize, filters: this._activeFilters(),
    })).subscribe({
      next: r => {
        this.items = r.results ?? [];
        this.totalCount = r.totalCount ?? 0;
        this._cdr.markForCheck();
      },
    });
  }

  onFilterChange(): void {
    this._filter$.next();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get rangeStart(): number {
    return this.totalCount === 0 ? 0 : (this.pageIndex - 1) * this.pageSize + 1;
  }

  get rangeEnd(): number {
    return Math.min(this.pageIndex * this.pageSize, this.totalCount);
  }

  goToPage(page: number): void {
    const target = Math.min(Math.max(1, page), this.totalPages);
    if (target !== this.pageIndex) {
      this.pageIndex = target;
      this.load();
    }
  }

  prevPage(): void {
    this.goToPage(this.pageIndex - 1);
  }

  nextPage(): void {
    this.goToPage(this.pageIndex + 1);
  }

  changePageSize(size: number): void {
    this.pageSize = +size;
    this.pageIndex = 1;
    this.load();
  }

  openCreate(): void {
    this.editingId = null;
    this.form.reset(this._formDefaults);
    this.showForm = true;
    this.selectedTheaterIds.clear();
    this.selectedDays.clear();
    this.form.patchValue({ autoApply: false, applyToAllTheaters: true, isActive: true });
    this._syncCodeValidator();
    this._cdr.markForCheck();
  }

  edit(item: Dto): void {
    this.editingId = item.id ?? null;
    this.form.reset(this._formDefaults);
    this.selectedTheaterIds = new Set(item.theaterIds ?? []);
    this.selectedDays = new Set(this._maskToDays(item.daysOfWeekMask));
    this._syncCodeValidator();
    this.form.patchValue({
      ...item,
      startDate: item.startDate ? new Date(item.startDate).toISOString().split('T')[0] : '',
      endDate: item.endDate ? new Date(item.endDate).toISOString().split('T')[0] : '',
      startTimeOfDay: item.startTimeOfDay ?? '',
      endTimeOfDay: item.endTimeOfDay ?? '',
    });
    this.showForm = true;
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
    obs.subscribe({
      next: () => {
        this.load();
        this.cancelEdit();
      },
      error: e => { this._toastError(e, 'common.saveFailed'); },
    });
  }

  delete(id?: string): void {
    if (!id) {
      return;
    }
    this._pendingDeleteId = id;
    this.confirmOpen = true;
  }

  confirmDelete(): void {
    const id = this._pendingDeleteId;
    this.confirmOpen = false;
    this._pendingDeleteId = null;
    if (id) {
      this._svc.deleteDiscount(id).subscribe({
        next: () => this.load(),
        error: e => { this._toastError(e, 'common.deleteFailed'); },
      });
    }
  }

  cancelEdit(): void {
    this.showForm = false;
    this.editingId = null;
    this.form.reset(this._formDefaults);
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

  typeName(id?: string): string {
    return this.discountTypes.find(t => t.id === id)?.name ?? '—';
  }

  scopeLabel(x: Dto): string {
    if (x.applyToAllTheaters) return this._translate.instant('discounts.systemWide');
    return this._translate.instant('discounts.nTheaters', { n: x.theaterIds?.length ?? 0 });
  }

  private _toastError(err: unknown, fallbackKey: string): void {
    this._toast.error(apiErrorMessage(err, this._translate.instant(fallbackKey)));
    this._cdr.markForCheck();
  }

  private _activeFilters(): Record<string, string> {
    const out: Record<string, string> = {};
    for (const key of Object.keys(this.filters)) {
      const value = (this.filters[key] ?? '').trim();
      if (value) {
        out[key] = value;
      }
    }
    return out;
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
    if (!mask) return [];
    return this.weekDays.map(d => d.value).filter(d => (mask & (1 << d)) !== 0);
  }

  private _syncCodeValidator(): void {
    const code = this.form.get('code')!;
    code.setValidators(this.form.value.autoApply ? [] : [Validators.required]);
    code.updateValueAndValidity({ emitEvent: false });
  }
}
