import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import {
  CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException,
  PromotionMechanicValues, PromotionEligibilityValues, PromotionCategoryValues, SalesChannel, SalesChannelValues, DayOfWeekValues,
  daysMaskToList, daysListToMask, channelsMaskToList, channelsListToMask,
} from 'CinemaLib';
import { ImageUploadService } from '../../../../shared/image-upload.service';

type Dto = CinemaServiceAgent.PromotionProgramDTO;
type Mechanic = CinemaServiceAgent.PromotionMechanic;
type Eligibility = CinemaServiceAgent.PromotionEligibility;

export interface PromotionProgramDialogData {
  program: Dto | null;
}

/** Create/edit form for a PromotionProgram (Cinestar-style promo card), opened via MatDialog.
 * Resolves `true` on save, `false`/`undefined` on cancel. */
@Component({
  selector: 'app-promotion-program-dialog',
  standalone: false,
  templateUrl: './promotion-program.dialog.html',
  styleUrl: './promotion-program.dialog.scss',
})
export class PromotionProgramDialog implements OnInit {
  readonly editingId: string | null;
  form: FormGroup;

  readonly mechanics = PromotionMechanicValues;
  readonly eligibilities = PromotionEligibilityValues;
  readonly categories = PromotionCategoryValues;
  readonly channels = SalesChannelValues;
  readonly weekDays = DayOfWeekValues;
  readonly formats = [
    { value: null, name: 'promoProgram.anyFormat' },
    { value: CinemaServiceAgent.ProjectionForm.TwoD, name: '2D' },
    { value: CinemaServiceAgent.ProjectionForm.ThreeD, name: '3D' },
  ];
  readonly seatKinds = [
    { value: null, name: 'promoProgram.anySeatKind' },
    { value: CinemaServiceAgent.SeatKind.Standard, name: 'promoProgram.seatStandard' },
    { value: CinemaServiceAgent.SeatKind.Double, name: 'promoProgram.seatDouble' },
  ];

  memberShips: CinemaServiceAgent.MemberShipDTO[] = [];
  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  movies: CinemaServiceAgent.MovieDTO[] = [];

  /** Day-of-week selections per schedule row index (mask stored as a Set for the chip UI). */
  scheduleDaySelections: Set<number>[] = [];
  /** Sales-channel selections, shared across the whole program. */
  channelSelection = new Set<number>();

  uploading = false;
  uploadError = '';

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _upload: ImageUploadService,
    private _cdr: ChangeDetectorRef,
    private _dialogRef: MatDialogRef<PromotionProgramDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) private _data: PromotionProgramDialogData,
  ) {
    this.editingId = this._data.program?.id ?? null;
    const item = this._data.program;

    this.form = this._fb.group({
      title: [item?.title ?? '', [Validators.required, Validators.maxLength(200)]],
      summary: [item?.summary ?? ''],
      bannerUrl: [item?.bannerUrl ?? ''],
      category: [item?.category ?? CinemaServiceAgent.PromotionCategory.Promotion],
      displayOrder: [item?.displayOrder ?? 0],
      isPublished: [item?.isPublished ?? false],

      mechanic: [item?.mechanic ?? CinemaServiceAgent.PromotionMechanic.Info],
      percentOff: [item?.percentOff ?? null],

      eligibility: [item?.eligibility ?? CinemaServiceAgent.PromotionEligibility.Everyone],
      minMemberShipId: [item?.minMemberShipId ?? null],
      maxAge: [item?.maxAge ?? null],

      startDate: [item?.startDate ? new Date(item.startDate).toISOString().split('T')[0] : '', Validators.required],
      endDate: [item?.endDate ? new Date(item.endDate).toISOString().split('T')[0] : '', Validators.required],

      excludeHolidays: [item?.excludeHolidays ?? false],
      maxTicketsPerTransaction: [item?.maxTicketsPerTransaction ?? null],
      stacksWithMembershipDiscount: [item?.stacksWithMembershipDiscount ?? false],
      termsText: [item?.termsText ?? ''],

      autoApply: [item?.autoApply ?? false],
      code: [item?.code ?? ''],
      maxUsage: [item?.maxUsage ?? null],
      maxDiscountAmount: [item?.maxDiscountAmount ?? null],
      movieId: [item?.movieId ?? null],

      prices: this._fb.array((item?.prices ?? []).map(p => this._priceRow(p.projectionForm ?? null, p.seatKind ?? null, p.price ?? null))),
      schedules: this._fb.array([]),
    });

    this.channelSelection = new Set(channelsMaskToList(item?.allowedChannelsMask));
    (item?.schedules ?? []).forEach(s => this._addScheduleRow(s));

    const resetAutoApplyIfNowDisabled = () => {
      if (this.autoApplyDisabled && this.form.value.autoApply) {
        this.form.get('autoApply')!.setValue(false, { emitEvent: false });
      }
      this._cdr.markForCheck();
    };
    this.form.get('mechanic')!.valueChanges.subscribe(resetAutoApplyIfNowDisabled);
    this.form.get('eligibility')!.valueChanges.subscribe(resetAutoApplyIfNowDisabled);
    // A program is triggered by exactly one of Code or AutoApply — entering a code turns AutoApply off.
    this.form.get('code')!.valueChanges.subscribe(value => {
      if (value?.trim() && this.form.value.autoApply) {
        this.form.get('autoApply')!.setValue(false, { emitEvent: false });
      }
      this._cdr.markForCheck();
    });
  }

  ngOnInit(): void {
    this._svc.getMemberShips(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.memberShips = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.theaters = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.movies = r.results ?? []; this._cdr.markForCheck(); });
  }

  // ── Derived state for the template ──────────────────────────────────────────
  get mechanic(): Mechanic { return this.form.value.mechanic; }
  get eligibility(): Eligibility { return this.form.value.eligibility; }
  get isFixedPrice(): boolean { return this.mechanic === CinemaServiceAgent.PromotionMechanic.FixedPrice; }
  get isPercentOff(): boolean { return this.mechanic === CinemaServiceAgent.PromotionMechanic.PercentOff; }
  get isMembers(): boolean { return this.eligibility === CinemaServiceAgent.PromotionEligibility.Members; }
  get isAgeLimit(): boolean { return this.eligibility === CinemaServiceAgent.PromotionEligibility.AgeLimit; }
  get autoApplyDisabled(): boolean {
    return this.mechanic === CinemaServiceAgent.PromotionMechanic.Info
      || this.eligibility === CinemaServiceAgent.PromotionEligibility.StudentOrTeacher
      || this.eligibility === CinemaServiceAgent.PromotionEligibility.AgeLimit
      || !!this.form.value.code?.trim();
  }

  get priceRows(): FormArray { return this.form.get('prices') as FormArray; }
  get scheduleRows(): FormArray { return this.form.get('schedules') as FormArray; }

  // ── Price rows ───────────────────────────────────────────────────────────────
  addPriceRow(): void {
    this.priceRows.push(this._priceRow(null, null, null));
  }

  removePriceRow(index: number): void {
    this.priceRows.removeAt(index);
  }

  private _priceRow(projectionForm: CinemaServiceAgent.ProjectionForm | null, seatKind: CinemaServiceAgent.SeatKind | null, price: number | null): FormGroup {
    return this._fb.group({
      projectionForm: [projectionForm],
      seatKind: [seatKind],
      price: [price, [Validators.required, Validators.min(0)]],
    });
  }

  // ── Schedule rows ────────────────────────────────────────────────────────────
  addScheduleRow(): void {
    this._addScheduleRow(null);
  }

  removeScheduleRow(index: number): void {
    this.scheduleRows.removeAt(index);
    this.scheduleDaySelections.splice(index, 1);
  }

  toggleScheduleDay(rowIndex: number, day: CinemaServiceAgent.DayOfWeek): void {
    const set = this.scheduleDaySelections[rowIndex];
    if (set.has(day)) { set.delete(day); } else { set.add(day); }
    this._cdr.markForCheck();
  }

  isScheduleDaySelected(rowIndex: number, day: CinemaServiceAgent.DayOfWeek): boolean {
    return this.scheduleDaySelections[rowIndex]?.has(day) ?? false;
  }

  private _addScheduleRow(existing: CinemaServiceAgent.PromotionProgramScheduleDTO | null): void {
    this.scheduleRows.push(this._fb.group({
      theaterId: [existing?.theaterId ?? null],
      startTimeOfDay: [existing?.startTimeOfDay ?? ''],
      endTimeOfDay: [existing?.endTimeOfDay ?? ''],
    }));
    this.scheduleDaySelections.push(new Set(daysMaskToList(existing?.daysOfWeekMask)));
  }

  // ── Channels ─────────────────────────────────────────────────────────────────
  toggleChannel(value: SalesChannel): void {
    if (this.channelSelection.has(value)) { this.channelSelection.delete(value); } else { this.channelSelection.add(value); }
    this._cdr.markForCheck();
  }

  isChannelSelected(value: SalesChannel): boolean {
    return this.channelSelection.has(value);
  }

  // ── Banner upload ────────────────────────────────────────────────────────────
  onPickImage(event: Event): void {
    this._uploadImage(
      event,
      url => this.form.patchValue({ bannerUrl: url }),
      (uploading, error) => { this.uploading = uploading; this.uploadError = error; },
    );
  }

  clearBanner(): void {
    this.form.patchValue({ bannerUrl: '' });
  }

  private _uploadImage(event: Event, onUrl: (url: string) => void, setState: (uploading: boolean, error: string) => void): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) {
      return;
    }
    setState(true, '');
    this._upload.upload(file).subscribe({
      next: url => {
        onUrl(url);
        setState(false, '');
        this._cdr.markForCheck();
      },
      error: () => {
        setState(false, 'promotionPrograms.uploadFailed');
        this._cdr.markForCheck();
      },
    });
  }

  save(): void {
    if (!this.form.valid || this.priceRows.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const payload = this._payload();
    const obs = this.editingId
      ? this._svc.updatePromotionProgram(CinemaServiceAgent.UpdatePromotionProgramRequest.fromJS({ ...payload, id: this.editingId }))
      : this._svc.createPromotionProgram(CinemaServiceAgent.CreatePromotionProgramRequest.fromJS(payload));

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

  private _payload() {
    const v = this.form.value;
    const prices = this.isFixedPrice
      ? this.priceRows.value.map((p: any) => ({ projectionForm: p.projectionForm ?? null, seatKind: p.seatKind ?? null, price: p.price }))
      : [];
    const schedules = this.scheduleRows.value.map((s: any, i: number) => ({
      theaterId: s.theaterId || null,
      daysOfWeekMask: daysListToMask([...this.scheduleDaySelections[i]]),
      startTimeOfDay: s.startTimeOfDay || null,
      endTimeOfDay: s.endTimeOfDay || null,
    }));

    const code = v.code?.trim() || null;
    return {
      title: v.title.trim(),
      summary: v.summary?.trim() || null,
      bannerUrl: v.bannerUrl?.trim() || null,
      category: v.category,
      displayOrder: v.displayOrder ?? 0,
      isPublished: !!v.isPublished,
      mechanic: v.mechanic,
      percentOff: this.isPercentOff ? v.percentOff : null,
      eligibility: v.eligibility,
      minMemberShipId: this.isMembers ? v.minMemberShipId : null,
      maxAge: this.isAgeLimit ? v.maxAge : null,
      startDate: v.startDate,
      endDate: v.endDate,
      excludeHolidays: !!v.excludeHolidays,
      allowedChannelsMask: channelsListToMask([...this.channelSelection]),
      maxTicketsPerTransaction: v.maxTicketsPerTransaction || null,
      stacksWithMembershipDiscount: !!v.stacksWithMembershipDiscount,
      termsText: v.termsText?.trim() || null,
      // A program is triggered by exactly one of Code or AutoApply — a code wins when both are set.
      autoApply: code ? false : (this.autoApplyDisabled ? false : !!v.autoApply),
      code,
      maxUsage: v.maxUsage || null,
      maxDiscountAmount: v.maxDiscountAmount || null,
      movieId: v.movieId || null,
      prices,
      schedules,
    };
  }
}
