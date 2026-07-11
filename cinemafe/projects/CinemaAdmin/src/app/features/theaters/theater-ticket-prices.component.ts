import { Component, Input, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog/catalog-crud.base';
import { ModalComponent } from '../../shared/modal.component';
import { ConfirmModalComponent } from '../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.TicketPriceDTO;

/** Ticket-price management scoped to a single theater: explicit price per seat type × time slot × holiday. */
@Component({
  selector: 'app-theater-ticket-prices',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './theater-ticket-prices.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterTicketPricesComponent extends CatalogCrudBase<Dto> {
  @Input({ required: true }) theaterId!: string;
  private _svc = inject(CinemaServiceAgent.HttpService);

  seatTypes: CinemaServiceAgent.SeatTypeDTO[] = [];
  timeSlots: CinemaServiceAgent.TimeSlotDTO[] = [];

  override ngOnInit(): void {
    super.ngOnInit();
    const search = CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500, filters: { theaterId: this.theaterId } });
    this._svc.getSeatTypes(search).subscribe(r => { this.seatTypes = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getTimeSlots(search).subscribe(r => { this.timeSlots = r.results ?? []; this._cdr.markForCheck(); });
  }

  buildForm() {
    return this._fb.group({
      seatTypeId: ['', Validators.required],
      timeSlotId: ['', Validators.required],
      isHoliday: [false],
      price: [0, [Validators.required, Validators.min(0)]],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getTicketPrices(CinemaServiceAgent.PagingSearchDTO.fromJS(
      { pageIndex, pageSize, filters: { ...filters, theaterId: this.theaterId } }));
  }
  create(v: any) { return this._svc.createTicketPrice(CinemaServiceAgent.CreateTicketPriceRequest.fromJS({ ...v, theaterId: this.theaterId })); }
  update(v: any, id: string) { return this._svc.updateTicketPrice(CinemaServiceAgent.UpdateTicketPriceRequest.fromJS({ ...v, id, theaterId: this.theaterId })); }
  remove(id: string) { return this._svc.deleteTicketPrice(id); }

  seatTypeName(id?: string): string {
    return this.seatTypes.find(s => s.id === id)?.name ?? '—';
  }
  timeSlotName(id?: string): string {
    const t = this.timeSlots.find(s => s.id === id);
    return t ? `${t.name} (${t.startTime}–${t.endTime})` : '—';
  }
}
