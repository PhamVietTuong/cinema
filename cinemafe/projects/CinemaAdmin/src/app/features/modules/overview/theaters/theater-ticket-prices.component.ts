import { ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CinemaServiceAgent } from 'CinemaLib';

type Dto = CinemaServiceAgent.TicketPriceDTO;

/**
 * Ticket-price management scoped to a single theater: a pricing multiplier per seat type ×
 * time slot × holiday, applied to the showtime's own base price (not an absolute amount).
 */
@Component({
  selector: 'app-theater-ticket-prices',
  standalone: false,
  templateUrl: './theater-ticket-prices.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterTicketPricesComponent implements OnInit {
  @Input({ required: true }) theaterId!: string;

  items: Dto[] = [];

  showForm = false;
  editingId: string | null = null;
  form: FormGroup;
  private readonly _formDefaults: unknown;

  confirmOpen = false;
  private _pendingDeleteId: string | null = null;

  roomTypes: CinemaServiceAgent.RoomTypeDTO[] = [];
  seatTypes: CinemaServiceAgent.SeatTypeDTO[] = [];
  timeSlots: CinemaServiceAgent.TimeSlotDTO[] = [];

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
  ) {
    this.form = this._fb.group({
      roomTypeId: ['', Validators.required],
      seatTypeId: ['', Validators.required],
      timeSlotId: ['', Validators.required],
      isHoliday: [false],
      priceMultiplier: [1, [Validators.required, Validators.min(0.01)]],
    });
    this._formDefaults = this.form.getRawValue();
  }

  ngOnInit(): void {
    this.load();
    const search = CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200, filters: { theaterId: this.theaterId } });
    this._svc.getRoomTypes(search).subscribe(r => { this.roomTypes = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getSeatTypes(search).subscribe(r => { this.seatTypes = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getTimeSlots(search).subscribe(r => { this.timeSlots = r.results ?? []; this._cdr.markForCheck(); });
  }

  load(): void {
    this._svc.getTicketPrices(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 10, filters: { theaterId: this.theaterId },
    })).subscribe(r => {
      this.items = r.results ?? [];
      this._cdr.markForCheck();
    });
  }

  openCreate(): void {
    this.editingId = null;
    this.form.reset(this._formDefaults);
    this.showForm = true;
  }

  edit(item: Dto): void {
    this.editingId = item.id ?? null;
    this.form.reset(this._formDefaults);
    this.form.patchValue(item);
    this.showForm = true;
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateTicketPrice(CinemaServiceAgent.UpdateTicketPriceRequest.fromJS({ ...v, id: this.editingId, theaterId: this.theaterId }))
      : this._svc.createTicketPrice(CinemaServiceAgent.CreateTicketPriceRequest.fromJS({ ...v, theaterId: this.theaterId }));
    obs.subscribe(() => {
      this.load();
      this.cancelEdit();
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
      this._svc.deleteTicketPrice(id).subscribe(() => this.load());
    }
  }

  cancelEdit(): void {
    this.showForm = false;
    this.editingId = null;
    this.form.reset(this._formDefaults);
  }

  roomTypeName(id?: string): string {
    return this.roomTypes.find(t => t.id === id)?.name ?? '—';
  }
  seatTypeName(id?: string): string {
    return this.seatTypes.find(s => s.id === id)?.name ?? '—';
  }
  timeSlotName(id?: string): string {
    const t = this.timeSlots.find(s => s.id === id);
    return t ? `${t.name} (${t.startTime}–${t.endTime})` : '—';
  }
}
