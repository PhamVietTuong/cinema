import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.RoomDTO;

@Component({
  selector: 'app-rooms',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './rooms.component.html',
  styleUrl: './rooms.component.scss',
})
export class RoomsManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  readonly statuses = [
    { v: CinemaServiceAgent.RoomStatus.Active, label: 'Hoạt Động' },
    { v: CinemaServiceAgent.RoomStatus.Maintenance, label: 'Bảo Trì' },
    { v: CinemaServiceAgent.RoomStatus.Inactive, label: 'Ngừng Hoạt Động' },
  ];

  // ── Seat-map editor popup ─────────────────────────────────────────────────────
  viewingRoom: Dto | null = null;
  seats: CinemaServiceAgent.RoomSeatDTO[] = [];
  allSeatTypes: CinemaServiceAgent.SeatTypeDTO[] = [];
  seatsLoading = false;
  saving = false;
  /** Editor mode: paint a seat type onto seats, or pair/unpair double seats. */
  mode: 'paint' | 'pair' = 'paint';
  activeSeatTypeId = '';
  /** First seat picked while pairing; the next click completes the pair. */
  private _pairFirst: CinemaServiceAgent.RoomSeatDTO | null = null;

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

  // ── Seat-map editor ───────────────────────────────────────────────────────────
  /** Opens the editor: loads the room's seat grid and the seat-type palette. */
  editSeats(room: Dto): void {
    this.viewingRoom = room;
    this.seats = [];
    this.mode = 'paint';
    this._pairFirst = null;
    this.seatsLoading = true;
    this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .subscribe(r => {
        this.allSeatTypes = r.results ?? [];
        this.activeSeatTypeId = this.allSeatTypes[0]?.id ?? '';
        this._cdr.markForCheck();
      });
    this._svc.getRoomSeatMap(room.id!).subscribe({
      next: r => { this.seats = r ?? []; this.seatsLoading = false; this._cdr.markForCheck(); },
      error: () => { this.seatsLoading = false; this._cdr.markForCheck(); },
    });
  }

  closeSeats(): void {
    this.viewingRoom = null;
    this.seats = [];
    this._pairFirst = null;
  }

  setMode(m: 'paint' | 'pair'): void { this.mode = m; this._pairFirst = null; }

  /** Click handler: paint the active seat type, or pair/unpair two seats. */
  onSeatClick(seat: CinemaServiceAgent.RoomSeatDTO): void {
    if (this.mode === 'paint') {
      const t = this.allSeatTypes.find(x => x.id === this.activeSeatTypeId);
      if (!t) { return; }
      seat.seatTypeId = t.id;
      seat.seatTypeName = t.name;
      seat.seatTypeColor = t.color;
      seat.priceMultiplier = t.priceMultiplier;
      return;
    }

    // Pair mode: clicking a grouped seat unpairs the whole group.
    if (seat.seatGroupId) {
      const gid = seat.seatGroupId;
      this.seats.filter(s => s.seatGroupId === gid).forEach(s => s.seatGroupId = undefined);
      this._pairFirst = null;
      return;
    }
    if (!this._pairFirst) { this._pairFirst = seat; return; }
    if (this._pairFirst === seat) { this._pairFirst = null; return; }
    // Complete a new pair (a "double seat") by giving both the same fresh group id.
    const gid = crypto.randomUUID();
    this._pairFirst.seatGroupId = gid;
    seat.seatGroupId = gid;
    this._pairFirst = null;
  }

  isPairPending(seat: CinemaServiceAgent.RoomSeatDTO): boolean { return this._pairFirst === seat; }

  saveSeatMap(): void {
    if (!this.viewingRoom) { return; }
    this.saving = true;
    const request = CinemaServiceAgent.SaveSeatMapRequest.fromJS({
      roomId: this.viewingRoom.id,
      seats: this.seats.map(s => ({
        seatId: s.id,
        seatTypeId: s.seatTypeId,
        seatGroupId: s.seatGroupId,
        isActive: s.isActive,
      })),
    });
    this._svc.saveRoomSeatMap(request).subscribe({
      next: () => { this.saving = false; this.closeSeats(); this._cdr.markForCheck(); },
      error: () => { this.saving = false; this._cdr.markForCheck(); },
    });
  }

  get seatRows(): string[] {
    return [...new Set(this.seats.map(s => s.rowName ?? ''))];
  }
  seatsInRow(row: string): CinemaServiceAgent.RoomSeatDTO[] {
    return this.seats.filter(s => s.rowName === row).sort((a, b) => (a.colIndex ?? 0) - (b.colIndex ?? 0));
  }
}
