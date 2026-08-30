import { ChangeDetectorRef, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { CinemaServiceAgent, RoomStatusValues } from 'CinemaLib';

type Dto = CinemaServiceAgent.RoomDTO;

/** Room management scoped to a single theater: list, create/update, and seat-map editor. */
@Component({
  selector: 'app-theater-rooms',
  standalone: false,
  templateUrl: './theater-rooms.component.html',
  styleUrl: './theater-rooms.component.scss',
})
export class TheaterRoomsComponent implements OnInit, OnDestroy {
  /** The theater whose rooms this list manages. */
  @Input({ required: true }) theaterId!: string;

  items: Dto[] = [];
  totalCount = 0;
  pageIndex = 1;
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 20, 50];
  filters: Record<string, string> = {};

  showForm = false;
  editingId: string | null = null;
  form: FormGroup;
  private readonly _formDefaults: unknown;

  confirmOpen = false;
  private _pendingDeleteId: string | null = null;

  private readonly _filter$ = new Subject<void>();
  private readonly _destroy$ = new Subject<void>();

  readonly statuses = RoomStatusValues;
  roomTypes: CinemaServiceAgent.RoomTypeDTO[] = [];

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

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
  ) {
    this.form = this._fb.group({
      name: ['', Validators.required],
      roomTypeId: ['', Validators.required],
      totalRows: [1, [Validators.required, Validators.min(1)]],
      totalColumns: [1, [Validators.required, Validators.min(1)]],
      status: [CinemaServiceAgent.RoomStatus.Active, Validators.required],
    });
    this._formDefaults = this.form.getRawValue();
  }

  ngOnInit(): void {
    this._filter$.pipe(debounceTime(300), takeUntil(this._destroy$)).subscribe(() => {
      this.pageIndex = 1;
      this.load();
    });
    this.load();
    this._svc.getRoomTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200, filters: { theaterId: this.theaterId } }))
      .subscribe(r => { this.roomTypes = r.results ?? []; this._cdr.markForCheck(); });
  }

  ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
  }

  load(): void {
    this._svc.getRooms(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: this.pageIndex, pageSize: this.pageSize, filters: { ...this._activeFilters(), theaterId: this.theaterId },
    })).subscribe(r => {
      this.items = r.results ?? [];
      this.totalCount = r.totalCount ?? 0;
      this._cdr.markForCheck();
    });
  }

  onFilterChange(): void {
    this._filter$.next();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get rangeStart(): number {
    return this.totalCount === 0 ? 0 : (this.pageIndex - 1) * this.pageSize + 1;
  }

  get rangeEnd(): number {
    return Math.min(this.pageIndex * this.pageSize, this.totalCount);
  }

  goToPage(page: number): void {
    const target = Math.min(Math.max(1, page), this.totalPages);
    if (target !== this.pageIndex) {
      this.pageIndex = target;
      this.load();
    }
  }

  prevPage(): void {
    this.goToPage(this.pageIndex - 1);
  }

  nextPage(): void {
    this.goToPage(this.pageIndex + 1);
  }

  changePageSize(size: number): void {
    this.pageSize = +size;
    this.pageIndex = 1;
    this.load();
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
      ? this._svc.updateRoom(CinemaServiceAgent.UpdateRoomRequest.fromJS({ ...v, id: this.editingId, theaterId: this.theaterId }))
      : this._svc.createRoom(CinemaServiceAgent.CreateRoomRequest.fromJS({ ...v, theaterId: this.theaterId }));
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
      this._svc.deleteRoom(id).subscribe(() => this.load());
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

  statusLabel(s?: CinemaServiceAgent.RoomStatus): string {
    return this.statuses.find(x => x.value === s)?.name ?? '—';
  }

  // ── Seat-map editor ───────────────────────────────────────────────────────────
  /** Opens the editor: loads the room's seat grid and the seat-type palette. */
  editSeats(room: Dto): void {
    this.viewingRoom = room;
    this.seats = [];
    this.mode = 'paint';
    this._pairFirst = null;
    this.seatsLoading = true;
    this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200, filters: { theaterId: this.theaterId } }))
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

  private _activeFilters(): Record<string, string> {
    const out: Record<string, string> = {};
    for (const key of Object.keys(this.filters)) {
      const value = (this.filters[key] ?? '').trim();
      if (value) {
        out[key] = value;
      }
    }
    return out;
  }
}
