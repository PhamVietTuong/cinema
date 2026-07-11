import { Component, Input, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog/catalog-crud.base';
import { ModalComponent } from '../../shared/modal.component';
import { ConfirmModalComponent } from '../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.RoomDTO;

/** Room management scoped to a single theater: list, create/update, and seat-map editor. */
@Component({
  selector: 'app-theater-rooms',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './theater-rooms.component.html',
  styleUrl: './theater-rooms.component.scss',
})
export class TheaterRoomsComponent extends CatalogCrudBase<Dto> {
  /** The theater whose rooms this list manages. */
  @Input({ required: true }) theaterId!: string;

  private _svc = inject(CinemaServiceAgent.HttpService);

  readonly statuses = [
    { v: CinemaServiceAgent.RoomStatus.Active, label: 'Hoạt Động' },
    { v: CinemaServiceAgent.RoomStatus.Maintenance, label: 'Bảo Trì' },
    { v: CinemaServiceAgent.RoomStatus.Inactive, label: 'Ngừng Hoạt Động' },
  ];
  roomTypes: CinemaServiceAgent.RoomTypeDTO[] = [];

  override ngOnInit(): void {
    super.ngOnInit();
    this._svc.getRoomTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500, filters: { theaterId: this.theaterId } }))
      .subscribe(r => { this.roomTypes = r.results ?? []; this._cdr.markForCheck(); });
  }

  roomTypeName(id?: string): string {
    return this.roomTypes.find(t => t.id === id)?.name ?? '—';
  }

  // ── Seat-map editor popup ─────────────────────────────────────────────────────
  viewingRoom: Dto | null = null;
  seats: CinemaServiceAgent.RoomSeatDTO[] = [];
  allSeatTypes: CinemaServiceAgent.SeatTypeDTO[] = [];
  seatsLoading = false;
  saving = false;
  resizing = false;
  /** Editor mode: paint a seat type onto seats, or pair/unpair double seats. */
  mode: 'paint' | 'pair' = 'paint';
  activeSeatTypeId = '';
  /** First seat picked while pairing; the next click completes the pair. */
  private _pairFirst: CinemaServiceAgent.RoomSeatDTO | null = null;

  buildForm() {
    return this._fb.group({
      name: ['', Validators.required],
      roomTypeId: ['', Validators.required],
      totalRows: [1, [Validators.required, Validators.min(1)]],
      totalColumns: [1, [Validators.required, Validators.min(1)]],
      status: [CinemaServiceAgent.RoomStatus.Active, Validators.required],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getRooms(CinemaServiceAgent.PagingSearchDTO.fromJS(
      { pageIndex, pageSize, filters: { ...filters, theaterId: this.theaterId } }));
  }
  create(v: any) { return this._svc.createRoom(CinemaServiceAgent.CreateRoomRequest.fromJS({ ...v, theaterId: this.theaterId })); }
  update(v: any, id: string) { return this._svc.updateRoom(CinemaServiceAgent.UpdateRoomRequest.fromJS({ ...v, id, theaterId: this.theaterId })); }
  remove(id: string) { return this._svc.deleteRoom(id); }

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
    this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500, filters: { theaterId: this.theaterId } }))
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

  // ── Grid resize: add/remove rows or columns, preserving existing seats ──────────
  addRow(): void { this.resizeGrid(1, 0); }
  removeRow(): void { this.resizeGrid(-1, 0); }
  addColumn(): void { this.resizeGrid(0, 1); }
  removeColumn(): void { this.resizeGrid(0, -1); }

  private resizeGrid(rowDelta: number, colDelta: number): void {
    if (!this.viewingRoom || this.resizing) { return; }
    const totalRows = (this.viewingRoom.totalRows ?? 0) + rowDelta;
    const totalColumns = (this.viewingRoom.totalColumns ?? 0) + colDelta;
    if (totalRows < 1 || totalColumns < 1) { return; }

    this.resizing = true;
    this._pairFirst = null;
    this._svc.resizeRoomSeatGrid(CinemaServiceAgent.ResizeSeatGridRequest.fromJS(
      { roomId: this.viewingRoom.id, totalRows, totalColumns }))
      .subscribe({
        next: seats => {
          this.seats = seats ?? [];
          this.viewingRoom!.totalRows = totalRows;
          this.viewingRoom!.totalColumns = totalColumns;
          this.resizing = false;
          this._cdr.markForCheck();
        },
        error: () => { this.resizing = false; this._cdr.markForCheck(); },
      });
  }

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
