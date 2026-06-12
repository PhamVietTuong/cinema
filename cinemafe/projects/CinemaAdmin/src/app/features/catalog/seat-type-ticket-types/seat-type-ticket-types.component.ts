import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';

type Dto = CinemaServiceAgent.SeatTypeTicketTypeDTO;

@Component({
  selector: 'app-seat-type-ticket-types',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './seat-type-ticket-types.component.html',
})
export class SeatTypeTicketTypesManagementComponent implements OnInit {
  private _svc = inject(CinemaServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);
  private _fb = inject(FormBuilder);

  items: Dto[] = [];
  seatTypes: CinemaServiceAgent.SeatTypeDTO[] = [];
  ticketTypes: CinemaServiceAgent.TicketTypeDTO[] = [];
  totalCount = 0;
  pageIndex = 1;
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 20, 50];
  showForm = false;
  /** True while editing an existing pair (key fields locked). */
  editing = false;
  form = this._fb.group({
    seatTypeId: ['', Validators.required],
    ticketTypeId: ['', Validators.required],
    priceMultiplier: [1, [Validators.required, Validators.min(0)]],
  });

  ngOnInit(): void {
    this.load();
    this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .subscribe(r => { this.seatTypes = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getTicketTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .subscribe(r => { this.ticketTypes = r.results ?? []; this._cdr.markForCheck(); });
  }

  load(): void {
    this._svc.getSeatTypeTicketTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: this.pageIndex, pageSize: this.pageSize }))
      .subscribe(r => { this.items = r.results ?? []; this.totalCount = r.totalCount ?? 0; this._cdr.markForCheck(); });
  }

  openCreate(): void { this.editing = false; this.showForm = true; this.form.reset({ priceMultiplier: 1 }); }
  edit(x: Dto): void {
    this.editing = true;
    this.showForm = true;
    this.form.reset({ seatTypeId: x.seatTypeId, ticketTypeId: x.ticketTypeId, priceMultiplier: x.priceMultiplier });
  }
  cancelEdit(): void { this.showForm = false; this.editing = false; this.form.reset({ priceMultiplier: 1 }); }

  save(): void {
    if (!this.form.valid) { this.form.markAllAsTouched(); return; }
    const v = this.form.value;
    const obs = this.editing
      ? this._svc.updateSeatTypeTicketType(CinemaServiceAgent.UpdateSeatTypeTicketTypeRequest.fromJS(v))
      : this._svc.createSeatTypeTicketType(CinemaServiceAgent.CreateSeatTypeTicketTypeRequest.fromJS(v));
    obs.subscribe({ next: () => { this.load(); this.cancelEdit(); } });
  }

  delete(seatTypeId?: string, ticketTypeId?: string): void {
    if (seatTypeId && ticketTypeId && confirm('Xóa cấu hình giá này?')) {
      this._svc.deleteSeatTypeTicketType(seatTypeId, ticketTypeId).subscribe({ next: () => this.load() });
    }
  }

  get totalPages(): number { return Math.max(1, Math.ceil(this.totalCount / this.pageSize)); }
  get rangeStart(): number { return this.totalCount === 0 ? 0 : (this.pageIndex - 1) * this.pageSize + 1; }
  get rangeEnd(): number { return Math.min(this.pageIndex * this.pageSize, this.totalCount); }
  goToPage(p: number): void { const t = Math.min(Math.max(1, p), this.totalPages); if (t !== this.pageIndex) { this.pageIndex = t; this.load(); } }
  prevPage(): void { this.goToPage(this.pageIndex - 1); }
  nextPage(): void { this.goToPage(this.pageIndex + 1); }
  changePageSize(s: number): void { this.pageSize = +s; this.pageIndex = 1; this.load(); }
}
