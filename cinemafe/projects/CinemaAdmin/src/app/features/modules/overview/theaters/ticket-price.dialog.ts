import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import {
  CinemaServiceAgent, DayOfWeekValues, DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';

type TimeSlotDTO = CinemaServiceAgent.TimeSlotDTO;
type TicketPriceDTO = CinemaServiceAgent.TicketPriceDTO;
type DayOfWeek = CinemaServiceAgent.DayOfWeek;

export interface TicketPriceDialogData {
  theaterId: string;
  roomTypeId: string;
  roomTypeName: string;
  timeSlot: TimeSlotDTO;
  day: DayOfWeek;
  existing?: TicketPriceDTO;
}

/**
 * Cell editor for the weekly pricing timeline: sets one multiplier across one or more days of week for
 * a (theater, room type, time slot) combination in a single batch call. Opened via MatDialog from a
 * timeline segment click. Resolves `true` on save, `false` on cancel.
 */
@Component({
  selector: 'app-ticket-price-dialog',
  standalone: false,
  templateUrl: './ticket-price.dialog.html',
  styleUrls: ['./ticket-price.dialog.scss'],
})
export class TicketPriceDialog {
  readonly dayOptions = DayOfWeekValues;
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogService: DialogService,
    private _dialogRef: MatDialogRef<TicketPriceDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) public data: TicketPriceDialogData,
  ) {
    this.form = this._fb.group({
      daysOfWeek: [[data.day], Validators.required],
      priceMultiplier: [data.existing?.priceMultiplier ?? 1, [Validators.required, Validators.min(0.01)]],
    });
  }

  toggleDay(day: DayOfWeek): void {
    const current: DayOfWeek[] = this.form.controls['daysOfWeek'].value ?? [];
    const next = current.includes(day) ? current.filter(d => d !== day) : [...current, day];
    this.form.controls['daysOfWeek'].setValue(next);
  }

  isDaySelected(day: DayOfWeek): boolean {
    return (this.form.controls['daysOfWeek'].value ?? []).includes(day);
  }

  adjustMultiplier(delta: number): void {
    const current = Number(this.form.controls['priceMultiplier'].value) || 1;
    const next = Math.max(0.05, Math.round((current + delta) * 100) / 100);
    this.form.controls['priceMultiplier'].setValue(next);
  }

  save(): void {
    if (!this.form.valid || !(this.form.value.daysOfWeek ?? []).length) {
      this.form.markAllAsTouched();
      return;
    }
    const request = CinemaServiceAgent.UpsertTicketPricesRequest.fromJS({
      theaterId: this.data.theaterId,
      roomTypeId: this.data.roomTypeId,
      timeSlotId: this.data.timeSlot.id,
      daysOfWeek: this.form.value.daysOfWeek,
      priceMultiplier: this.form.value.priceMultiplier,
    });

    this._store.dispatch(showLoading());
    this._svc.upsertTicketPrices(request).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  resetDefault(): void {
    if (!this.data.existing?.id) {
      return;
    }
    this._dialogService.openConfirmDialog({ message: 'theaters.ticketPrices.confirmReset' })
      .afterClosed().subscribe(confirmed => {
        if (confirmed) {
          this._resetConfirmed(this.data.existing!.id!);
        }
      });
  }

  private _resetConfirmed(id: string): void {
    this._store.dispatch(showLoading());
    this._svc.deleteTicketPrice(id).subscribe({
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
