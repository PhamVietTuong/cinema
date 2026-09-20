import { Component, Inject } from '@angular/core';
import { ChangeDetectorRef } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent } from 'CinemaLib';

export interface SeatMapDialogData {
  theaterId: string;
  room: CinemaServiceAgent.RoomDTO;
}

/** Seat-map editor for a room: grid resize, pair/unpair double seats. A seat has no stored kind of
 * its own — Standard vs Double is derived from whether it's paired. Resolves `true` if the map was
 * saved. */
@Component({
  selector: 'app-seat-map-dialog',
  standalone: false,
  templateUrl: './seat-map.dialog.html',
  styleUrl: './seat-map.dialog.scss',
})
export class SeatMapDialog {
  room: CinemaServiceAgent.RoomDTO;
  seats: CinemaServiceAgent.RoomSeatDTO[] = [];
  seatTypes: CinemaServiceAgent.SeatTypeDTO[] = [];
  seatsLoading = false;
  saving = false;
  resizing = false;
  /** True once a save has actually persisted, so the dialog resolves `true` on close. */
  private _saved = false;
  /** First seat picked while pairing; the next click completes the pair. */
  private _pairFirst: CinemaServiceAgent.RoomSeatDTO | null = null;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _cdr: ChangeDetectorRef,
    private _dialogRef: MatDialogRef<SeatMapDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: SeatMapDialogData,
  ) {
    this.room = data.room;
    this.seatsLoading = true;
    this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200, filters: { theaterId: data.theaterId } }))
      .subscribe(r => {
        this.seatTypes = (r.results ?? []).sort((a, b) => (a.kind ?? 0) - (b.kind ?? 0));
        this._cdr.markForCheck();
      });
    this._svc.getRoomSeatMap(this.room.id!).subscribe({
      next: r => { this.seats = r ?? []; this.seatsLoading = false; this._cdr.markForCheck(); },
      error: () => { this.seatsLoading = false; this._cdr.markForCheck(); },
    });
  }

  close(): void {
    this._dialogRef.close(this._saved);
  }

  colorFor(seat: CinemaServiceAgent.RoomSeatDTO): string {
    const kind = seat.isDouble ? CinemaServiceAgent.SeatKind.Double : CinemaServiceAgent.SeatKind.Standard;
    return this.seatTypes.find(t => t.kind === kind)?.color ?? '#8fa3bf';
  }

  nameFor(seat: CinemaServiceAgent.RoomSeatDTO): string {
    const kind = seat.isDouble ? CinemaServiceAgent.SeatKind.Double : CinemaServiceAgent.SeatKind.Standard;
    return this.seatTypes.find(t => t.kind === kind)?.name ?? '';
  }

  // ── Grid resize: add/remove rows or columns, preserving existing seats ──────────
  addRow(): void { this.resizeGrid(1, 0); }
  removeRow(): void { this.resizeGrid(-1, 0); }
  addColumn(): void { this.resizeGrid(0, 1); }
  removeColumn(): void { this.resizeGrid(0, -1); }

  private resizeGrid(rowDelta: number, colDelta: number): void {
    if (this.resizing) { return; }
    const totalRows = (this.room.totalRows ?? 0) + rowDelta;
    const totalColumns = (this.room.totalColumns ?? 0) + colDelta;
    if (totalRows < 1 || totalColumns < 1) { return; }

    this.resizing = true;
    this._pairFirst = null;
    this._svc.resizeRoomSeatGrid(CinemaServiceAgent.ResizeSeatGridRequest.fromJS(
      { roomId: this.room.id, totalRows, totalColumns }))
      .subscribe({
        next: seats => {
          this.seats = seats ?? [];
          this.room.totalRows = totalRows;
          this.room.totalColumns = totalColumns;
          this.resizing = false;
          this._saved = true;
          this._cdr.markForCheck();
        },
        error: () => { this.resizing = false; this._cdr.markForCheck(); },
      });
  }

  /** Click handler: pair/unpair two seats into a double seat. */
  onSeatClick(seat: CinemaServiceAgent.RoomSeatDTO): void {
    // Clicking a grouped seat unpairs the whole group.
    if (seat.seatGroupId) {
      const gid = seat.seatGroupId;
      this.seats.filter(s => s.seatGroupId === gid).forEach(s => { s.seatGroupId = undefined; s.isDouble = false; });
      this._pairFirst = null;
      return;
    }
    if (!this._pairFirst) { this._pairFirst = seat; return; }
    if (this._pairFirst === seat) { this._pairFirst = null; return; }
    // Complete a new pair (a "double seat") by giving both the same fresh group id.
    const gid = crypto.randomUUID();
    this._pairFirst.seatGroupId = gid;
    this._pairFirst.isDouble = true;
    seat.seatGroupId = gid;
    seat.isDouble = true;
    this._pairFirst = null;
  }

  isPairPending(seat: CinemaServiceAgent.RoomSeatDTO): boolean { return this._pairFirst === seat; }

  saveSeatMap(): void {
    this.saving = true;
    const request = CinemaServiceAgent.SaveSeatMapRequest.fromJS({
      roomId: this.room.id,
      seats: this.seats.map(s => ({
        seatId: s.id,
        seatGroupId: s.seatGroupId,
        isActive: s.isActive,
      })),
    });
    this._svc.saveRoomSeatMap(request).subscribe({
      next: () => { this.saving = false; this._saved = true; this.close(); },
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
