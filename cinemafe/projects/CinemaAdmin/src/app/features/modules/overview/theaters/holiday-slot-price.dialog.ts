import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import {
  CinemaServiceAgent, DialogService, SharedModule,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';

type TimeSlotDTO = CinemaServiceAgent.TimeSlotDTO;
type HolidayDTO = CinemaServiceAgent.HolidayDTO;
type HolidayTimeSlotPriceDTO = CinemaServiceAgent.HolidayTimeSlotPriceDTO;

export interface HolidaySlotPriceDialogData {
  theaterId: string;
  holiday: HolidayDTO;
  /** `yyyy-MM-dd` of the holiday occurrence being edited, for the dialog subtitle. */
  holidayDate: string;
  timeSlot: TimeSlotDTO;
  /** The slot's own override row, when it has one; absent means the holiday default applies. */
  existing?: HolidayTimeSlotPriceDTO;
  /** Late-night slots only: the holiday's factor on the slot before it (supplied by the parent). */
  precedingMultiplier?: number | null;
}

/**
 * Editor for one holiday's factor on one time slot, opened from a holiday segment of the weekly pricing
 * timeline. Saving writes a HolidayTimeSlotPrice override; "use default" removes it so the slot falls back
 * to the holiday's default factor. Resolves `true` on save, `false` on cancel.
 */
@Component({
  selector: 'app-holiday-slot-price-dialog',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './holiday-slot-price.dialog.html',
  // The stepper/hint/actions styles are the same ones the weekday cell editor uses.
  styleUrls: ['./ticket-price.dialog.scss'],
})
export class HolidaySlotPriceDialog {
  private readonly _svc = inject(CinemaServiceAgent.HttpService);
  private readonly _store = inject(Store);
  private readonly _dialogService = inject(DialogService);
  private readonly _dialogRef = inject<MatDialogRef<HolidaySlotPriceDialog, boolean>>(MatDialogRef);
  readonly data = inject<HolidaySlotPriceDialogData>(MAT_DIALOG_DATA);

  form: FormGroup = inject(FormBuilder).group({
    priceMultiplier: [
      this.data.existing?.priceMultiplier ?? this.data.holiday.priceMultiplier ?? 1,
      [Validators.required, Validators.min(0.05)],
    ],
  });

  /** Non-blocking hint: a late-night slot priced above the holiday's slot before it. */
  get showLateNightWarning(): boolean {
    const preceding = this.data.precedingMultiplier;
    const value = Number(this.form.controls['priceMultiplier'].value);
    return preceding != null && Number.isFinite(value) && value > preceding + 1e-9;
  }

  adjustMultiplier(delta: number): void {
    const current = Number(this.form.controls['priceMultiplier'].value) || 1;
    const next = Math.max(0.05, Math.round((current + delta) * 100) / 100);
    this.form.controls['priceMultiplier'].setValue(next);
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    this._upsert(Number(this.form.value.priceMultiplier));
  }

  useDefault(): void {
    if (!this.data.existing?.id) {
      return;
    }
    this._dialogService.openConfirmDialog({ message: 'theaters.ticketPrices.confirmHolidayReset' })
      .afterClosed().subscribe(confirmed => {
        if (confirmed) {
          this._upsert(null);
        }
      });
  }

  cancel(): void {
    this._dialogRef.close(false);
  }

  /** A null multiplier asks the server to remove this slot's override. */
  private _upsert(priceMultiplier: number | null): void {
    const request = CinemaServiceAgent.UpsertHolidayTimeSlotPricesRequest.fromJS({
      holidayId: this.data.holiday.id,
      theaterId: this.data.theaterId,
      items: [{ timeSlotId: this.data.timeSlot.id, priceMultiplier }],
    });
    this._store.dispatch(showLoading());
    this._svc.upsertHolidayTimeSlotPrices(request).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}
