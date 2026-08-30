import { ChangeDetectorRef, Component } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import {
  CinemaServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService,
  ToastService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';

type Dto = CinemaServiceAgent.CommentModerationDTO;

@Component({
  selector: 'app-comments',
  standalone: false,
  templateUrl: './comments.component.html',
})
export class CommentsModerationComponent extends BaseTableComponent {
  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _svc: CinemaServiceAgent.HttpService,
    private _dialogService: DialogService,
    private _toast: ToastService,
    private _translate: TranslateService,
  ) {
    super(cd, fb, router, store);
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({ approved: [''] });
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._svc.getCommentsForModeration(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  /** Approve (show) or hide a comment, then reload the current page. */
  moderate(id?: string, approved?: boolean): void {
    if (!id) {
      return;
    }
    this._svc.moderateComment(CinemaServiceAgent.ModerateCommentRequest.fromJS({ commentId: id, approved }))
      .subscribe({
        next: () => {
          this._toast.success(this._translate.instant(approved ? 'comments.approveSuccess' : 'comments.hideSuccess'));
          this.triggerSearch();
        },
        error: e => {
          this._toast.error(this._err(e, this._translate.instant('comments.moderateFailed')));
        },
      });
  }

  delete(id?: string): void {
    if (!id) {
      return;
    }
    this._dialogService.openConfirmDialog({ message: 'comments.confirmDelete' })
      .afterClosed().subscribe(confirmed => {
        if (confirmed) {
          this._deleteConfirmed(id);
        }
      });
  }

  private _deleteConfirmed(id: string): void {
    this._store.dispatch(showLoading());
    this._svc.deleteComment(CinemaServiceAgent.DeleteCommentRequest.fromJS({ commentId: id })).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.triggerSearch();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  private _err(e: any, fallback: string): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.error || x?.message || fallback);
  }
}
