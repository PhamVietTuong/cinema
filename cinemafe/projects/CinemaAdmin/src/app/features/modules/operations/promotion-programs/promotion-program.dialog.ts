import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import {
  CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException,
  PromotionMechanicValues, PromotionEligibilityValues, SalesChannel, SalesChannelValues, DayOfWeekValues,
  daysMaskToList, daysListToMask, channelsMaskToList, channelsListToMask,
} from 'CinemaLib';
import { ImageUploadService } from '../../../../shared/image-upload.service';

type Dto = CinemaServiceAgent.PromotionProgramDTO;
type Mechanic = CinemaServiceAgent.PromotionMechanic;
type Eligibility = CinemaServiceAgent.PromotionEligibility;
/** How a customer gets this program — mirrors the backend's Code-xor-AutoApply rule as a single
 * three-way choice instead of a toggle the user could leave in a confusing combination. */
type TriggerMode = 'none' | 'auto' | 'code';

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
  campaigns: CinemaServiceAgent.PromotionCampaignDTO[] = [];

  /** One step per former section — i18n keys reused as both section heading and step label. */
  readonly stepLabelKeys = [
    'promotionPrograms.sectionBasics',
    'promotionPrograms.sectionPricing',
    'promotionPrograms.sectionAudience',
    'promotionPrograms.sectionSchedule',
    'promotionPrograms.sectionRedemption',
  ];
  /** Which required/invalid-able controls live on each step, so save() can jump to the first
   * offending step instead of leaving the admin stuck on whichever step they happened to be on. */
  private readonly _stepControlNames = [
    ['title'],
    [],
    [],
    ['startDate', 'endDate'],
    ['code'],
  ];
  currentStep = 0;

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
      campaignId: [item?.campaignId ?? null],
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
      triggerMode: [(item?.code ? 'code' : item?.autoApply ? 'auto' : 'none') as TriggerMode],
      code: [item?.code ?? ''],
      maxUsage: [item?.maxUsage ?? null],
      maxDiscountAmount: [item?.maxDiscountAmount ?? null],
      movieId: [item?.movieId ?? null],

      prices: this._fb.array((item?.prices ?? []).map(p => this._priceRow(p.projectionForm ?? null, p.seatKind ?? null, p.price ?? null))),
      schedules: this._fb.array([]),
    });

    this.channelSelection = new Set(channelsMaskToList(item?.allowedChannelsMask));
    (item?.schedules ?? []).forEach(s => this._addScheduleRow(s));

    // The three-way triggerMode choice drives autoApply/code directly, so the xor rule is a UI
    // structure instead of a silently-corrected combination.
    this._applyTriggerMode(this.form.value.triggerMode, { emitEvent: false });
    this.form.get('triggerMode')!.valueChanges.subscribe((mode: TriggerMode) => this._applyTriggerMode(mode));

    // Auto-apply can become disallowed after the fact (switching Mechanic to Info, or Eligibility to
    // StudentOrTeacher/AgeLimit) — fall back to "not triggered online" rather than leaving a now-invalid
    // choice selected.
    const resetTriggerModeIfAutoNowDisallowed = () => {
      if (!this.autoApplyAllowed && this.form.value.triggerMode === 'auto') {
        this.form.get('triggerMode')!.setValue('none');
      }
      this._cdr.markForCheck();
    };
    this.form.get('mechanic')!.valueChanges.subscribe(resetTriggerModeIfAutoNowDisallowed);
    this.form.get('eligibility')!.valueChanges.subscribe(resetTriggerModeIfAutoNowDisallowed);
  }

  /** Keeps autoApply/code (the fields actually sent to the server) and the code control's
   * required-ness in sync with the triggerMode choice. */
  private _applyTriggerMode(mode: TriggerMode, opts: { emitEvent: boolean } = { emitEvent: false }): void {
    this.form.get('autoApply')!.setValue(mode === 'auto', opts);
    const codeCtrl = this.form.get('code')!;
    if (mode === 'code') {
      codeCtrl.setValidators([Validators.required]);
    } else {
      codeCtrl.setValue('', opts);
      codeCtrl.clearValidators();
    }
    codeCtrl.updateValueAndValidity({ emitEvent: false });
    this._cdr.markForCheck();
  }

  ngOnInit(): void {
    this._svc.getMemberShips(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.memberShips = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.theaters = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.movies = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getPromotionCampaigns(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.campaigns = r.results ?? []; this._cdr.markForCheck(); });
  }

  // ── Derived state for the template ──────────────────────────────────────────
  get mechanic(): Mechanic { return this.form.value.mechanic; }
  get eligibility(): Eligibility { return this.form.value.eligibility; }
  get isFixedPrice(): boolean { return this.mechanic === CinemaServiceAgent.PromotionMechanic.FixedPrice; }
  get isPercentOff(): boolean { return this.mechanic === CinemaServiceAgent.PromotionMechanic.PercentOff; }
  get isMembers(): boolean { return this.eligibility === CinemaServiceAgent.PromotionEligibility.Members; }
  get isAgeLimit(): boolean { return this.eligibility === CinemaServiceAgent.PromotionEligibility.AgeLimit; }
  get triggerMode(): TriggerMode { return this.form.value.triggerMode; }
  /** Whether Mechanic/Eligibility allow "Auto-apply" as a triggerMode choice (StudentOrTeacher/AgeLimit
   * have no online verification; Info is display-only) — independent of which mode is currently picked. */
  get autoApplyAllowed(): boolean {
    return this.mechanic !== CinemaServiceAgent.PromotionMechanic.Info
      && this.eligibility !== CinemaServiceAgent.PromotionEligibility.StudentOrTeacher
      && this.eligibility !== CinemaServiceAgent.PromotionEligibility.AgeLimit;
  }

  get priceRows(): FormArray { return this.form.get('prices') as FormArray; }
  get scheduleRows(): FormArray { return this.form.get('schedules') as FormArray; }

  // ── Step navigation ───────────────────────────────────────────────────────────
  get isFirstStep(): boolean { return this.currentStep === 0; }
  get isLastStep(): boolean { return this.currentStep === this.stepLabelKeys.length - 1; }

  goToStep(i: number): void {
    this.currentStep = i;
    this._cdr.markForCheck();
  }

  back(): void {
    if (!this.isFirstStep) {
      this.goToStep(this.currentStep - 1);
    }
  }

  next(): void {
    if (!this.isLastStep) {
      this.goToStep(this.currentStep + 1);
    }
  }

  stepDone(i: number): boolean { return i < this.currentStep; }

  /** First step with an invalid control, or -1 if every step is valid — lets save() land the
   * admin on the step that needs attention instead of a generic "fix the form" dead end. */
  private _firstInvalidStep(): number {
    for (let i = 0; i < this._stepControlNames.length; i++) {
      if (this._stepControlNames[i].some(name => this.form.get(name)?.invalid)) {
        return i;
      }
      if (i === 1 && this.isFixedPrice && this.priceRows.invalid) {
        return i;
      }
    }
    return -1;
  }

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
      const badStep = this._firstInvalidStep();
      if (badStep >= 0) {
        this.goToStep(badStep);
      }
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

    const mode: TriggerMode = v.triggerMode;
    return {
      title: v.title.trim(),
      summary: v.summary?.trim() || null,
      bannerUrl: v.bannerUrl?.trim() || null,
      campaignId: v.campaignId || null,
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
      // triggerMode is the single source of truth for Code-xor-AutoApply — see _applyTriggerMode.
      autoApply: mode === 'auto',
      code: mode === 'code' ? (v.code?.trim() || null) : null,
      maxUsage: v.maxUsage || null,
      maxDiscountAmount: v.maxDiscountAmount || null,
      movieId: v.movieId || null,
      prices,
      schedules,
    };
  }
}
