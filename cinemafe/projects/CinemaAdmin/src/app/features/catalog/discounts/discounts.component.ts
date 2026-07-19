import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.DiscountDTO;

@Component({
  selector: 'app-discounts',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './discounts.component.html',
  styles: [`
    .ad-hint { display: block; margin-top: 4px; font-size: 12px; color: var(--ad-muted); font-weight: 400; }
    .scope-checklist { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 6px 14px;
      margin-top: 8px; max-height: 180px; overflow-y: auto; padding: 8px; border: 1px solid var(--ad-border); border-radius: 6px; }
    .scope-days { display: flex; flex-wrap: wrap; gap: 6px 16px; margin-top: 8px; }
  `],
})
export class DiscountsManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);
  private _translate = inject(TranslateService);

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

  override ngOnInit(): void {
    super.ngOnInit();
    this._svc.getDiscountTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .subscribe(r => { this.discountTypes = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .subscribe(r => { this.theaters = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .subscribe(r => { this.movies = r.results ?? []; this._cdr.markForCheck(); });
  }

  buildForm() {
    return this._fb.group({
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
  }

  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getDiscounts(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters }));
  }
  create(v: any) { return this._svc.createDiscount(CinemaServiceAgent.CreateDiscountRequest.fromJS(this._payload(v))); }
  update(v: any, id: string) { return this._svc.updateDiscount(CinemaServiceAgent.UpdateDiscountRequest.fromJS({ ...this._payload(v), id })); }
  remove(id: string) { return this._svc.deleteDiscount(id); }

  override openCreate(): void {
    super.openCreate();
    this.selectedTheaterIds.clear();
    this.selectedDays.clear();
    this.form.patchValue({ autoApply: false, applyToAllTheaters: true, isActive: true });
    this._syncCodeValidator();
    this._cdr.markForCheck();
  }

  protected override toFormValue(i: Dto) {
    this.selectedTheaterIds = new Set(i.theaterIds ?? []);
    this.selectedDays = new Set(this._maskToDays(i.daysOfWeekMask));
    this._syncCodeValidator();
    return {
      ...i,
      startDate: i.startDate ? new Date(i.startDate).toISOString().split('T')[0] : '',
      endDate: i.endDate ? new Date(i.endDate).toISOString().split('T')[0] : '',
      startTimeOfDay: i.startTimeOfDay ?? '',
      endTimeOfDay: i.endTimeOfDay ?? '',
    };
  }

  // ── Scope UI helpers ──────────────────────────────────────────────────────────
  toggleTheater(id?: string): void {
    if (!id) return;
    this.selectedTheaterIds.has(id) ? this.selectedTheaterIds.delete(id) : this.selectedTheaterIds.add(id);
    this._cdr.markForCheck();
  }
  isTheaterSelected(id?: string): boolean {
    return !!id && this.selectedTheaterIds.has(id);
  }
  toggleDay(value: number): void {
    this.selectedDays.has(value) ? this.selectedDays.delete(value) : this.selectedDays.add(value);
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
