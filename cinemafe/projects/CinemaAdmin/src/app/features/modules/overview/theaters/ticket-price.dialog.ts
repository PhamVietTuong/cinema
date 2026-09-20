import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.TicketPriceDTO;

export interface TicketPriceDialogData {
  theaterId: string;
  ticketPrice: Dto | null;
  roomTypes: CinemaServiceAgent.RoomTypeDTO[];
  timeSlots: CinemaServiceAgent.TimeSlotDTO[];
}

/** Create/edit form for a ticket price, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-ticket-price-dialog',
  standalone: false,
  templateUrl: './ticket-price.dialog.html',
})
export class TicketPriceDialog {
  readonly editingId: string | null;
  form: FormGroup;
  readonly roomTypes: CinemaServiceAgent.RoomTypeDTO[];
  readonly timeSlots: CinemaServiceAgent.TimeSlotDTO[];

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<TicketPriceDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) private _data: TicketPriceDialogData,
  ) {
    this.roomTypes = _data.roomTypes;
    this.timeSlots = _data.timeSlots;
    this.editingId = _data.ticketPrice?.id ?? null;
    this.form = this._fb.group({
      roomTypeId: [_data.ticketPrice?.roomTypeId ?? '', Validators.required],
      timeSlotId: [_data.ticketPrice?.timeSlotId ?? '', Validators.required],
      isHoliday: [_data.ticketPrice?.isHoliday ?? false],
      priceMultiplier: [_data.ticketPrice?.priceMultiplier ?? 1, [Validators.required, Validators.min(0.01)]],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateTicketPrice(CinemaServiceAgent.UpdateTicketPriceRequest.fromJS({ ...v, id: this.editingId, theaterId: this._data.theaterId }))
      : this._svc.createTicketPrice(CinemaServiceAgent.CreateTicketPriceRequest.fromJS({ ...v, theaterId: this._data.theaterId }));

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
