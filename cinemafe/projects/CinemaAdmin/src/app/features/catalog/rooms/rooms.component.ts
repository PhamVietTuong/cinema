import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent, PaymentServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';

type Dto = CinemaServiceAgent.RoomDTO;

@Component({
  selector: 'app-rooms',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './rooms.component.html',
  styleUrl: './rooms.component.scss',
})
export class RoomsManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);
  private _payment = inject(PaymentServiceAgent.HttpService);

  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  readonly statuses = [
    { v: CinemaServiceAgent.RoomStatus.Active, label: 'Hoạt Động' },
    { v: CinemaServiceAgent.RoomStatus.Maintenance, label: 'Bảo Trì' },
    { v: CinemaServiceAgent.RoomStatus.Inactive, label: 'Ngừng Hoạt Động' },
  ];

  // ── Seating-chart popup ───────────────────────────────────────────────────────
  readonly SeatStatus = PaymentServiceAgent.SeatStatus;
  viewingRoom: Dto | null = null;
  seats: PaymentServiceAgent.SeatDTO[] = [];
  seatsLoading = false;

  override ngOnInit(): void {
    super.ngOnInit();
    this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .subscribe(r => { this.theaters = r.results ?? []; this._cdr.markForCheck(); });
  }

  buildForm() {
    return this._fb.group({
      name: ['', Validators.required],
      theaterId: ['', Validators.required],
      totalRows: [1, [Validators.required, Validators.min(1)]],
      totalColumns: [1, [Validators.required, Validators.min(1)]],
      status: [CinemaServiceAgent.RoomStatus.Active, Validators.required],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getRooms(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters }));
  }
  create(v: any) { return this._svc.createRoom(CinemaServiceAgent.CreateRoomRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateRoom(CinemaServiceAgent.UpdateRoomRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteRoom(id); }

  theaterName(id?: string): string {
    return this.theaters.find(t => t.id === id)?.name ?? '—';
  }
  statusLabel(s?: CinemaServiceAgent.RoomStatus): string {
    return this.statuses.find(x => x.v === s)?.label ?? '—';
  }

  // ── Seating-chart popup ───────────────────────────────────────────────────────
  viewSeats(room: Dto): void {
    this.viewingRoom = room;
    this.seats = [];
    this.seatsLoading = true;
    this._payment.getSeats(PaymentServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1000, filters: { roomId: room.id } }))
      .subscribe({
        next: r => { this.seats = r.results ?? []; this.seatsLoading = false; this._cdr.markForCheck(); },
        error: () => { this.seatsLoading = false; this._cdr.markForCheck(); },
      });
  }

  closeSeats(): void {
    this.viewingRoom = null;
    this.seats = [];
  }

  get seatRows(): string[] {
    return [...new Set(this.seats.map(s => s.rowName ?? ''))];
  }
  seatsInRow(row: string): PaymentServiceAgent.SeatDTO[] {
    return this.seats.filter(s => s.rowName === row).sort((a, b) => (a.colIndex ?? 0) - (b.colIndex ?? 0));
  }
  get seatTypes(): { name: string; color: string }[] {
    const map = new Map<string, string>();
    for (const s of this.seats) {
      if (s.seatTypeName && !map.has(s.seatTypeName)) { map.set(s.seatTypeName, s.seatTypeColor || '#8fa3bf'); }
    }
    return [...map].map(([name, color]) => ({ name, color }));
  }
}
