import { ChangeDetectorRef, Component, Input } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import {
  CinemaServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService, RoomStatusValues,
} from 'CinemaLib';
import { RoomDialog } from './room.dialog';
import { SeatMapDialog } from './seat-map.dialog';

type Dto = CinemaServiceAgent.RoomDTO;

/** Room management scoped to a single theater: list, create/update, and seat-map editor. */
@Component({
  selector: 'app-theater-rooms',
  standalone: false,
  templateUrl: './theater-rooms.component.html',
  styleUrl: './theater-rooms.component.scss',
})
export class TheaterRoomsComponent extends BaseTableComponent {
  /** The theater whose rooms this list manages. */
  @Input({ required: true }) theaterId!: string;

  readonly statuses = RoomStatusValues;
  roomTypes: CinemaServiceAgent.RoomTypeDTO[] = [];

  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _svc: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _dialogService: DialogService,
  ) {
    super(cd, fb, router, store);
  }

  override ngOnInit(): void {
    super.ngOnInit();
    this._svc.getRoomTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200, filters: { theaterId: this.theaterId } }))
      .subscribe(r => { this.roomTypes = r.results ?? []; this._cd.markForCheck(); });
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({ name: [''] });
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._svc.getRooms(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  protected override _extraFilters(): Record<string, unknown> {
    return { theaterId: this.theaterId };
  }

  protected override _searchStateKey(): string {
    return this._router.url + '#rooms';
  }

  openCreate(): void {
    this._dialog.open(RoomDialog, { width: '600px', maxWidth: '100vw', data: { theaterId: this.theaterId, room: null, roomTypes: this.roomTypes } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  edit(item: Dto): void {
    this._dialog.open(RoomDialog, { width: '600px', maxWidth: '100vw', data: { theaterId: this.theaterId, room: item, roomTypes: this.roomTypes } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  openSeatMap(room: Dto): void {
    this._dialog.open(SeatMapDialog, { width: '90vw', maxWidth: '1100px', data: { theaterId: this.theaterId, room } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  delete(id?: string): void {
    if (!id) {
      return;
    }
    this._dialogService.openConfirmDialog({ message: 'common.confirmDelete' })
      .afterClosed().subscribe(confirmed => {
        if (confirmed) {
          this._deleteConfirmed(id);
        }
      });
  }

  private _deleteConfirmed(id: string): void {
    this._svc.deleteRoom(id).subscribe(() => this.triggerSearch());
  }

  roomTypeName(id?: string): string {
    return this.roomTypes.find(t => t.id === id)?.name ?? '—';
  }

  statusLabel(s?: CinemaServiceAgent.RoomStatus): string {
    return this.statuses.find(x => x.value === s)?.name ?? '—';
  }
}
