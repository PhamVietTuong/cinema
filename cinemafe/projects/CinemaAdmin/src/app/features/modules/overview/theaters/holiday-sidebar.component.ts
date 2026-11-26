import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { Store } from '@ngrx/store';
import { MatDialog } from '@angular/material/dialog';
import {
  CinemaServiceAgent, DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { HolidayDialog } from '../../categories/holidays/holiday.dialog';
import { HolidaySlotPriceDialog } from './holiday-slot-price.dialog';
import { HolidaySegmentVm, holidayIso, holidaySlotSegments, precedingHolidayMultiplier } from './ticket-price-timeline';

type Dto = CinemaServiceAgent.HolidayDTO;
type TimeSlotDTO = CinemaServiceAgent.TimeSlotDTO;
type HolidayTimeSlotPriceDTO = CinemaServiceAgent.HolidayTimeSlotPriceDTO;

interface HolidayRowVm {
  holiday: Dto;
  chips: HolidaySegmentVm[];
}

/**
 * Upcoming-holidays list shown beside the weekly pricing timeline. Holidays are global (not
 * per-theater), so editing or removing one here affects every theater — the template says so.
 * Reuses the existing Holiday CRUD dialog and endpoints. Each holiday also lists its per-time-slot factors
 * as chips, so a holiday's slot prices can be set ahead of time, before it enters the timeline's 7-day window.
 */
@Component({
  selector: 'app-holiday-sidebar',
  standalone: false,
  templateUrl: './holiday-sidebar.component.html',
  styleUrls: ['./holiday-sidebar.component.scss'],
})
export class HolidaySidebarComponent implements OnChanges {
  @Input() holidays: Dto[] = [];
  @Input() theaterId = '';
  @Input() timeSlots: TimeSlotDTO[] = [];
  @Input() holidayPrices: HolidayTimeSlotPriceDTO[] = [];
  @Output() changed = new EventEmitter<void>();

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _store: Store<any>,
    private _dialog: MatDialog,
    private _dialogService: DialogService,
  ) {}

  rows: HolidayRowVm[] = [];

  ngOnChanges(): void {
    this.rows = this.holidays.map(holiday => ({
      holiday,
      chips: holidaySlotSegments(this.timeSlots, holiday, this.holidayPrices),
    }));
  }

  editSlot(holiday: Dto, chip: HolidaySegmentVm): void {
    const existing = chip.rowId ? this.holidayPrices.find(p => p.id === chip.rowId) : undefined;
    this._dialog.open(HolidaySlotPriceDialog, {
      width: '480px',
      maxWidth: '100vw',
      data: {
        theaterId: this.theaterId,
        holiday,
        holidayDate: holidayIso(holiday),
        timeSlot: chip.timeSlot,
        existing,
        precedingMultiplier: precedingHolidayMultiplier(this.timeSlots, holiday, this.holidayPrices, chip.timeSlot),
      },
    }).afterClosed().subscribe(saved => { if (saved) { this.changed.emit(); } });
  }

  add(): void {
    this._dialog.open(HolidayDialog, { width: '560px', maxWidth: '100vw', data: { holiday: null } })
      .afterClosed().subscribe(saved => { if (saved) { this.changed.emit(); } });
  }

  edit(holiday: Dto): void {
    this._dialog.open(HolidayDialog, { width: '560px', maxWidth: '100vw', data: { holiday } })
      .afterClosed().subscribe(saved => { if (saved) { this.changed.emit(); } });
  }

  remove(id?: string): void {
    if (!id) {
      return;
    }
    this._dialogService.openConfirmDialog({ message: 'common.confirmDelete' })
      .afterClosed().subscribe(confirmed => {
        if (confirmed) {
          this._removeConfirmed(id);
        }
      });
  }

  private _removeConfirmed(id: string): void {
    this._store.dispatch(showLoading());
    this._svc.deleteHoliday(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.changed.emit();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}
