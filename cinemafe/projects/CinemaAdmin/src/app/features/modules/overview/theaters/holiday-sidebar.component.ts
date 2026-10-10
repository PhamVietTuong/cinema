import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Store } from '@ngrx/store';
import { MatDialog } from '@angular/material/dialog';
import {
  CinemaServiceAgent, DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { HolidayDialog } from '../../categories/holidays/holiday.dialog';

type Dto = CinemaServiceAgent.HolidayDTO;

/**
 * Upcoming-holidays list shown beside the weekly pricing timeline. Holidays are global (not
 * per-theater), so editing or removing one here affects every theater — the template says so.
 * Reuses the existing Holiday CRUD dialog and endpoints; this component only adds the sidebar
 * presentation used by the pricing timeline.
 */
@Component({
  selector: 'app-holiday-sidebar',
  standalone: false,
  templateUrl: './holiday-sidebar.component.html',
  styleUrls: ['./holiday-sidebar.component.scss'],
})
export class HolidaySidebarComponent {
  @Input() holidays: Dto[] = [];
  @Output() changed = new EventEmitter<void>();

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _store: Store<any>,
    private _dialog: MatDialog,
    private _dialogService: DialogService,
  ) {}

  add(): void {
    this._dialog.open(HolidayDialog, { width: '560px', data: { holiday: null } })
      .afterClosed().subscribe(saved => { if (saved) { this.changed.emit(); } });
  }

  edit(holiday: Dto): void {
    this._dialog.open(HolidayDialog, { width: '560px', data: { holiday } })
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
