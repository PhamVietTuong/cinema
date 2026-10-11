import { ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { Store } from '@ngrx/store';
import { MatDialog } from '@angular/material/dialog';
import {
  CinemaServiceAgent, MultiplierBand, MultiplierBandSpecs,
  showLoading, hideLoading, showException,
} from 'CinemaLib';
import { TicketPriceDialog } from './ticket-price.dialog';
import { buildWeekRows, todayIsoLocal, TimelineSegmentVm, WeekRowVm } from './ticket-price-timeline';

type RoomTypeDTO = CinemaServiceAgent.RoomTypeDTO;
type TimeSlotDTO = CinemaServiceAgent.TimeSlotDTO;
type TicketPriceDTO = CinemaServiceAgent.TicketPriceDTO;
type HolidayDTO = CinemaServiceAgent.HolidayDTO;
type DayOfWeek = CinemaServiceAgent.DayOfWeek;

/**
 * Weekly pricing timeline for a theater: one segmented bar per weekday (Mon→Sun), showing the
 * TicketPrice multiplier per time slot for the selected room type, plus a holidays sidebar. A weekday
 * that a Holiday falls on (within the next 7 days) renders as a full-width override banner instead.
 */
@Component({
  selector: 'app-theater-ticket-prices',
  standalone: false,
  templateUrl: './theater-ticket-prices.component.html',
  styleUrls: ['./theater-catalog-tab.scss', './theater-ticket-prices.component.scss'],
})
export class TheaterTicketPricesComponent implements OnInit {
  @Input({ required: true }) theaterId!: string;

  roomTypes: RoomTypeDTO[] = [];
  timeSlots: TimeSlotDTO[] = [];
  selectedRoomTypeId: string | null = null;

  prices: TicketPriceDTO[] = [];
  holidays: HolidayDTO[] = [];
  weekRows: WeekRowVm[] = [];
  invalidSlots: TimeSlotDTO[] = [];
  readonly legend = MultiplierBandSpecs;
  readonly legendBands: MultiplierBand[] = ['low', 'standard', 'peak', 'holiday'];
  readonly rulerHours = [0, 4, 8, 12, 16, 20, 24];
  readonly showRegularFor = new Set<DayOfWeek>();

  constructor(
    private _cd: ChangeDetectorRef,
    private _svc: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _store: Store<any>,
  ) {}

  ngOnInit(): void {
    const search = CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200, filters: { theaterId: this.theaterId } });
    this._svc.getRoomTypes(search).subscribe(r => {
      this.roomTypes = r.results ?? [];
      this.selectedRoomTypeId ??= this.roomTypes[0]?.id ?? null;
      this._loadPrices();
      this._cd.markForCheck();
    });
    this._svc.getTimeSlots(search).subscribe(r => {
      this.timeSlots = r.results ?? [];
      this._rebuild();
      this._cd.markForCheck();
    });
    this._loadHolidays();
  }

  selectRoomType(id: string): void {
    if (id === this.selectedRoomTypeId) {
      return;
    }
    this.selectedRoomTypeId = id;
    this._loadPrices();
  }

  openCell(row: WeekRowVm, segment: TimelineSegmentVm): void {
    if (!this.selectedRoomTypeId) {
      return;
    }
    const roomTypeName = this.roomTypes.find(rt => rt.id === this.selectedRoomTypeId)?.name ?? '';
    const existing = segment.rowId ? this.prices.find(p => p.id === segment.rowId) : undefined;
    this._dialog.open(TicketPriceDialog, {
      width: '480px',
      data: {
        theaterId: this.theaterId,
        roomTypeId: this.selectedRoomTypeId,
        roomTypeName,
        timeSlot: segment.timeSlot,
        day: row.day,
        existing,
      },
    }).afterClosed().subscribe(saved => { if (saved) { this._loadPrices(); } });
  }

  toggleRegular(day: DayOfWeek): void {
    if (this.showRegularFor.has(day)) {
      this.showRegularFor.delete(day);
    } else {
      this.showRegularFor.add(day);
    }
  }

  onHolidaysChanged(): void {
    this._loadHolidays();
  }

  private _loadPrices(): void {
    if (!this.selectedRoomTypeId) {
      this.prices = [];
      this._rebuild();
      return;
    }
    this._store.dispatch(showLoading());
    this._svc.getTicketPrices(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 200,
      filters: { theaterId: this.theaterId, roomTypeId: this.selectedRoomTypeId },
    })).subscribe({
      next: r => {
        this.prices = r.results ?? [];
        this._rebuild();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  private _loadHolidays(): void {
    this._svc.getHolidays(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 100,
      filters: { dateFrom: todayIsoLocal() },
      sort: { field: 'date', ascending: true },
    })).subscribe({
      next: r => {
        this.holidays = r.results ?? [];
        this._rebuild();
      },
      error: error => this._store.dispatch(showException({ error })),
    });
  }

  private _rebuild(): void {
    const { rows, invalidSlots } = buildWeekRows(this.timeSlots, this.prices, this.holidays, todayIsoLocal());
    this.weekRows = rows;
    this.invalidSlots = invalidSlots;
    this._cd.markForCheck();
  }

  get invalidSlotNames(): string {
    return this.invalidSlots.map(s => s.name).join(', ');
  }
}
