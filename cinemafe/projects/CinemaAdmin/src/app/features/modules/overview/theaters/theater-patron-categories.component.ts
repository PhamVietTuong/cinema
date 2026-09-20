import { ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import {
  CinemaServiceAgent,
  DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { PatronCategoryDialog } from './patron-category.dialog';

type Dto = CinemaServiceAgent.PatronCategoryDTO;

/** One logical category (e.g. "Adult") grouping its per-seat-kind rows (Standard/Double). */
export interface PatronCategoryGroup {
  name: string;
  description?: string;
  isActive: boolean;
  rows: Dto[];
}

/** Patron-category pricing management scoped to a single theater. Each logical category (Adult/
 * Student/...) is stored as one row per seat kind (Standard/Double) — this tab groups them back
 * together for display and editing. The dataset is small (a handful of categories x 2 kinds), so
 * it's loaded in full rather than paged. */
@Component({
  selector: 'app-theater-patron-categories',
  standalone: false,
  templateUrl: './theater-patron-categories.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterPatronCategoriesComponent implements OnInit {
  @Input({ required: true }) theaterId!: string;

  seatTypes: CinemaServiceAgent.SeatTypeDTO[] = [];
  groups: PatronCategoryGroup[] = [];
  loading = false;

  constructor(
    private _cd: ChangeDetectorRef,
    private _svc: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _dialogService: DialogService,
    private _store: Store<any>,
  ) {}

  ngOnInit(): void {
    this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 100, filters: { theaterId: this.theaterId },
    })).subscribe(r => {
      this.seatTypes = (r.results ?? []).sort((a, b) => (a.kind ?? 0) - (b.kind ?? 0));
      this._cd.markForCheck();
    });
    this._load();
  }

  private _load(): void {
    this.loading = true;
    this._cd.markForCheck();
    this._svc.getPatronCategoriesByTheater(this.theaterId).subscribe({
      next: rows => {
        this.groups = this._groupByName(rows ?? []);
        this.loading = false;
        this._cd.markForCheck();
      },
      error: () => {
        this.loading = false;
        this._cd.markForCheck();
      },
    });
  }

  private _groupByName(rows: Dto[]): PatronCategoryGroup[] {
    const byName = new Map<string, Dto[]>();
    for (const row of rows) {
      const key = row.name ?? '';
      const list = byName.get(key) ?? [];
      list.push(row);
      byName.set(key, list);
    }
    return Array.from(byName.entries())
      .map(([name, groupRows]) => ({
        name,
        description: groupRows[0]?.description,
        isActive: groupRows[0]?.isActive ?? true,
        rows: groupRows,
      }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  /** Null means this category has no row for that seat kind (cannot book it at all). */
  priceFor(group: PatronCategoryGroup, seatType: CinemaServiceAgent.SeatTypeDTO): number | null {
    const row = group.rows.find(r => r.seatTypeId === seatType.id);
    return row ? (row.price ?? 0) : null;
  }

  openCreate(): void {
    this._dialog.open(PatronCategoryDialog, { width: '560px', data: { theaterId: this.theaterId, seatTypes: this.seatTypes, categoryRows: null } })
      .afterClosed().subscribe(saved => { if (saved) { this._load(); } });
  }

  edit(group: PatronCategoryGroup): void {
    this._dialog.open(PatronCategoryDialog, { width: '560px', data: { theaterId: this.theaterId, seatTypes: this.seatTypes, categoryRows: group.rows } })
      .afterClosed().subscribe(saved => { if (saved) { this._load(); } });
  }

  delete(group: PatronCategoryGroup): void {
    const id = group.rows[0]?.id;
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
    this._svc.deletePatronCategory(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this._load();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}
