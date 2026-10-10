import { Component, Inject } from '@angular/core';
import { ChangeDetectorRef } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { CinemaServiceAgent, DialogService, SeatCell, seatCellsByRow, seatLabel, showException } from 'CinemaLib';

export interface SeatMapDialogData {
  theaterId: string;
  room: CinemaServiceAgent.RoomDTO;
}

/** Which action clicking a seat currently applies, picked from the palette at the top. */
type SeatTool = 'standard' | 'double' | 'remove';

/** Seat-map editor for a room: grid resize, set each seat's type by picking a seat type at the top
 * (paint mode) then clicking seats to apply it. A seat has no stored kind of its own — Standard vs
 * Double is derived from whether it's paired: applying Double auto-pairs the seat with an adjacent
 * unpaired seat in the same row; applying Standard on a paired seat unpairs the whole group.
 * Resolves `true` if the map was saved. */
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
  removing = false;
  /** The tool currently selected at the top of the dialog; clicking a seat applies it. */
  activeTool: SeatTool = 'standard';
  /** Shown briefly when a seat can't be paired (no free adjacent seat in its row). */
  pairError = false;
  /** True once a save has actually persisted, so the dialog resolves `true` on close. */
  private _saved = false;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _cdr: ChangeDetectorRef,
    private _store: Store<any>,
    private _dialogService: DialogService,
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
    return this.colorForKind(!!seat.isDouble);
  }

  colorForKind(isDouble: boolean): string {
    const kind = isDouble ? CinemaServiceAgent.SeatKind.Double : CinemaServiceAgent.SeatKind.Standard;
    return this.seatTypes.find(t => t.kind === kind)?.color ?? '#8fa3bf';
  }

  nameFor(seat: CinemaServiceAgent.RoomSeatDTO): string {
    return this.seatTypeName(!!seat.isDouble);
  }

  /** The boxes to draw for a row: a double seat's two physical seats merge into one wide cell,
   * matching the customer-facing seat map. Click/pairing logic still acts on the physical seats. */
  cellsInRow(row: string): SeatCell<CinemaServiceAgent.RoomSeatDTO>[] {
    return seatCellsByRow(this.seats, row);
  }

  cellLabel(cell: SeatCell<CinemaServiceAgent.RoomSeatDTO>): string {
    return cell.seats.map(seatLabel).join(' → ');
  }

  /** The theater's actual SeatType name for a kind (e.g. renamed to "Ghế VIP"), not a hardcoded
   * "Standard"/"Double" label — the seat-type menu must reflect what the admin configured. */
  seatTypeName(isDouble: boolean): string {
    const kind = isDouble ? CinemaServiceAgent.SeatKind.Double : CinemaServiceAgent.SeatKind.Standard;
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

  /** Click handler for a seat cell: applies whichever tool is currently active at the top. */
  onSeatClick(seat: CinemaServiceAgent.RoomSeatDTO): void {
    if (this.activeTool === 'remove') {
      this.confirmRemoveSeat(seat);
      return;
    }
    this.setSeatKind(seat, this.activeTool === 'double');
  }

  /** Sets a seat's type. Setting Double auto-pairs the seat with an adjacent unpaired seat in the
   * same row (prefers the seat to the right, else the left); setting Standard on a paired seat
   * unpairs the whole group. */
  private setSeatKind(seat: CinemaServiceAgent.RoomSeatDTO, isDouble: boolean): void {
    this.pairError = false;
    if (!isDouble) {
      if (seat.seatGroupId) {
        const gid = seat.seatGroupId;
        this.seats.filter(s => s.seatGroupId === gid).forEach(s => { s.seatGroupId = undefined; s.isDouble = false; });
      }
      return;
    }

    if (seat.seatGroupId) {
      return;
    }
    const rowSeats = this.seatsInRow(seat.rowName ?? '');
    const index = rowSeats.indexOf(seat);
    const right = rowSeats[index + 1];
    const left = rowSeats[index - 1];
    const partner = (right && !right.seatGroupId) ? right : (left && !left.seatGroupId) ? left : undefined;
    if (!partner) {
      this.pairError = true;
      return;
    }
    const gid = crypto.randomUUID();
    seat.seatGroupId = gid;
    seat.isDouble = true;
    partner.seatGroupId = gid;
    partner.isDouble = true;
  }

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
      next: () => {
        this.saving = false;
        this._saved = true;
        // Flush the "saving" binding synchronously before closing — MatDialog's close() triggers
        // its own change-detection pass on this still-attached view, and without this the pending
        // saving=false mutation trips NG0100 (ExpressionChangedAfterItHasBeenCheckedError).
        this._cdr.detectChanges();
        this.close();
      },
      error: () => { this.saving = false; this._cdr.markForCheck(); },
    });
  }

  // ── Remove a single seat, shifting the rest of its row down by one ColIndex ─────

  private confirmRemoveSeat(seat: CinemaServiceAgent.RoomSeatDTO): void {
    if (this.removing || this.resizing) {
      return;
    }
    const isPaired = !!seat.seatGroupId;
    this._dialogService.openConfirmDialog({
      message: isPaired ? 'theaters.rooms.confirmRemoveDoubleSeat' : 'theaters.rooms.confirmRemoveSeat',
    }).afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.removeSeat(seat);
      }
    });
  }

  private removeSeat(seat: CinemaServiceAgent.RoomSeatDTO): void {
    this.removing = true;
    // Flush synchronously: this runs inside the confirm dialog's own afterClosed() callback, right as
    // its closing animation finishes a change-detection pass on this still-attached view. Without this,
    // the pending removing=true mutation is observed mid-digest and trips NG0100
    // (ExpressionChangedAfterItHasBeenCheckedError) on the `busy` binding — the same hazard saveSeatMap()
    // works around before closing the dialog.
    this._cdr.detectChanges();
    // Preserve unsaved pairing edits made before this removal, by seat id, so "Save" afterward still
    // persists them — the server only knows about the last-saved grouping.
    const localGroups = new Map(this.seats.map(s => [s.id!, { seatGroupId: s.seatGroupId, isDouble: s.isDouble }]));

    this._svc.removeRoomSeat(CinemaServiceAgent.RemoveSeatRequest.fromJS({ roomId: this.room.id, seatId: seat.id }))
      .subscribe({
        next: seats => {
          const merged = seats ?? [];
          for (const s of merged) {
            const edit = localGroups.get(s.id!);
            if (edit) {
              s.seatGroupId = edit.seatGroupId;
              s.isDouble = edit.isDouble;
            }
          }
          // A group that lost its other half (its partner was the seat just removed) is no longer
          // a valid pair — same rule SaveSeatMapAsync applies server-side.
          const groupCounts = new Map<string, number>();
          for (const s of merged) {
            if (s.seatGroupId) {
              groupCounts.set(s.seatGroupId, (groupCounts.get(s.seatGroupId) ?? 0) + 1);
            }
          }
          for (const s of merged) {
            if (s.seatGroupId && groupCounts.get(s.seatGroupId) !== 2) {
              s.seatGroupId = undefined;
              s.isDouble = false;
            }
          }

          this.seats = merged;
          this._saved = true;
          this.removing = false;
          // Same NG0100 hazard as above: the HTTP response can land while a CDK overlay (the confirm
          // dialog's backdrop, or the snackbar on the error path) is mid-detach and already checking
          // this view, so flush synchronously rather than waiting for the next tick.
          this._cdr.detectChanges();
        },
        error: error => {
          this.removing = false;
          this._store.dispatch(showException({ error }));
          this._cdr.detectChanges();
        },
      });
  }

  get seatRows(): string[] {
    return [...new Set(this.seats.map(s => s.rowName ?? ''))];
  }
  seatsInRow(row: string): CinemaServiceAgent.RoomSeatDTO[] {
    return this.seats.filter(s => s.rowName === row).sort((a, b) => (a.colIndex ?? 0) - (b.colIndex ?? 0));
  }
}
