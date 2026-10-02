import { ChangeDetectorRef, Component, Input } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import {
  CinemaServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { TicketPriceDialog } from './ticket-price.dialog';

type Dto = CinemaServiceAgent.TicketPriceDTO;

/**
 * Ticket-price management scoped to a single theater: a pricing multiplier per room type ×
 * time slot × holiday, applied to the resolved PatronCategory price (not an absolute amount).
 */
@Component({
  selector: 'app-theater-ticket-prices',
  standalone: false,
  templateUrl: './theater-ticket-prices.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterTicketPricesComponent extends BaseTableComponent<Dto> {
  @Input({ required: true }) theaterId!: string;

  roomTypes: CinemaServiceAgent.RoomTypeDTO[] = [];
  timeSlots: CinemaServiceAgent.TimeSlotDTO[] = [];

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
    const search = CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200, filters: { theaterId: this.theaterId } });
    this._svc.getRoomTypes(search).subscribe(r => { this.roomTypes = r.results ?? []; this._cd.markForCheck(); });
    this._svc.getTimeSlots(search).subscribe(r => { this.timeSlots = r.results ?? []; this._cd.markForCheck(); });
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({});
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._svc.getTicketPrices(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  protected override _extraFilters(): Record<string, unknown> {
    return { theaterId: this.theaterId };
  }

  protected override _searchStateKey(): string {
    return this._router.url + '#ticketPrices';
  }

  openCreate(): void {
    this._dialog.open(TicketPriceDialog, {
      width: '600px',
      data: { theaterId: this.theaterId, ticketPrice: null, roomTypes: this.roomTypes, timeSlots: this.timeSlots },
    }).afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  edit(item: Dto): void {
    this._dialog.open(TicketPriceDialog, {
      width: '600px',
      data: { theaterId: this.theaterId, ticketPrice: item, roomTypes: this.roomTypes, timeSlots: this.timeSlots },
    }).afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
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
    this._store.dispatch(showLoading());
    this._svc.deleteTicketPrice(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.triggerSearch();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  roomTypeName(id?: string): string {
    return this.roomTypes.find(t => t.id === id)?.name ?? '—';
  }
  timeSlotName(id?: string): string {
    const t = this.timeSlots.find(s => s.id === id);
    return t ? `${t.name} (${t.startTime}–${t.endTime})` : '—';
  }
}
