import { ChangeDetectorRef, Component, Input } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import {
  CinemaServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
} from 'CinemaLib';
import { SeatTypeDialog } from './seat-type.dialog';

type Dto = CinemaServiceAgent.SeatTypeDTO;

/** Seat-type management scoped to a single theater. Every theater has exactly Standard + Double,
 * seeded on theater creation — this tab is edit-only (Name/Description/Color), no create/delete. */
@Component({
  selector: 'app-theater-seat-types',
  standalone: false,
  templateUrl: './theater-seat-types.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterSeatTypesComponent extends BaseTableComponent {
  @Input({ required: true }) theaterId!: string;

  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _svc: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
  ) {
    super(cd, fb, router, store);
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({});
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  protected override _extraFilters(): Record<string, unknown> {
    return { theaterId: this.theaterId };
  }

  protected override _searchStateKey(): string {
    return this._router.url + '#seatTypes';
  }

  edit(item: Dto): void {
    this._dialog.open(SeatTypeDialog, { width: '560px', data: { theaterId: this.theaterId, seatType: item } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }
}
