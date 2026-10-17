import { ChangeDetectorRef, Component } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import {
  CinemaServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService,
  PromotionMechanicValues, PromotionEligibilityValues,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { PromotionProgramDialog } from './promotion-program.dialog';

type Dto = CinemaServiceAgent.PromotionProgramDTO;

@Component({
  selector: 'app-promotion-programs',
  standalone: false,
  templateUrl: './promotion-programs.component.html',
})
export class PromotionProgramsManagementComponent extends BaseTableComponent {
  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _svc: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _dialogService: DialogService,
    private _translate: TranslateService,
  ) {
    super(cd, fb, router, store);
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({ keyword: [''], isPublished: [''] });
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._svc.getPromotionPrograms(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  openCreate(): void {
    this._dialog.open(PromotionProgramDialog, { width: '820px', maxWidth: '95vw', data: { program: null } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  edit(item: Dto): void {
    this._dialog.open(PromotionProgramDialog, { width: '820px', maxWidth: '95vw', data: { program: item } })
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

  mechanicName(value?: CinemaServiceAgent.PromotionMechanic): string {
    const key = PromotionMechanicValues.find(x => x.value === value)?.name;
    return key ? this._translate.instant(key) : '—';
  }

  eligibilityName(value?: CinemaServiceAgent.PromotionEligibility): string {
    const key = PromotionEligibilityValues.find(x => x.value === value)?.name;
    return key ? this._translate.instant(key) : '—';
  }

  private _deleteConfirmed(id: string): void {
    this._store.dispatch(showLoading());
    this._svc.deletePromotionProgram(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.triggerSearch();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}
