import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';
import { CinemaServiceAgent, ToastService } from 'CinemaLib';

type Dto = CinemaServiceAgent.CommentModerationDTO;

@Component({
  selector: 'app-comments',
  standalone: false,
  templateUrl: './comments.component.html',
})
export class CommentsModerationComponent implements OnInit, OnDestroy {
  items: Dto[] = [];
  totalCount = 0;
  pageIndex = 1;
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 20, 50];
  filters: Record<string, string> = {};

  confirmOpen = false;
  private _pendingDeleteId: string | null = null;

  private readonly _filter$ = new Subject<void>();
  private readonly _destroy$ = new Subject<void>();

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _cdr: ChangeDetectorRef,
    private _toast: ToastService,
    private _translate: TranslateService,
  ) {}

  ngOnInit(): void {
    this._filter$.pipe(debounceTime(300), takeUntil(this._destroy$)).subscribe(() => {
      this.pageIndex = 1;
      this.load();
    });
    this.load();
  }

  ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
  }

  load(): void {
    this._svc.getCommentsForModeration(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: this.pageIndex, pageSize: this.pageSize, filters: this._activeFilters(),
    })).subscribe({
      next: r => {
        this.items = r.results ?? [];
        this.totalCount = r.totalCount ?? 0;
        this._cdr.markForCheck();
      },
    });
  }

  onFilterChange(): void {
    this._filter$.next();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get rangeStart(): number {
    return this.totalCount === 0 ? 0 : (this.pageIndex - 1) * this.pageSize + 1;
  }

  get rangeEnd(): number {
    return Math.min(this.pageIndex * this.pageSize, this.totalCount);
  }

  goToPage(page: number): void {
    const target = Math.min(Math.max(1, page), this.totalPages);
    if (target !== this.pageIndex) {
      this.pageIndex = target;
      this.load();
    }
  }

  prevPage(): void {
    this.goToPage(this.pageIndex - 1);
  }

  nextPage(): void {
    this.goToPage(this.pageIndex + 1);
  }

  changePageSize(size: number): void {
    this.pageSize = +size;
    this.pageIndex = 1;
    this.load();
  }

  /** Approve (show) or hide a comment, then reload the current page. */
  moderate(id?: string, approved?: boolean): void {
    if (!id) { return; }
    this._svc.moderateComment(CinemaServiceAgent.ModerateCommentRequest.fromJS({ commentId: id, approved }))
      .subscribe({
        next: () => {
          this._toast.success(this._translate.instant(approved ? 'comments.approveSuccess' : 'comments.hideSuccess'));
          this.load();
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
    this._pendingDeleteId = id;
    this.confirmOpen = true;
  }

  confirmDelete(): void {
    const id = this._pendingDeleteId;
    this.confirmOpen = false;
    this._pendingDeleteId = null;
    if (id) {
      this._svc.deleteComment(CinemaServiceAgent.DeleteCommentRequest.fromJS({ commentId: id })).subscribe({
        next: () => this.load(),
        error: e => { this._toast.error(this._err(e, this._translate.instant('common.deleteFailed'))); },
      });
    }
  }

  private _err(e: any, fallback: string): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.error || x?.message || fallback);
  }

  private _activeFilters(): Record<string, string> {
    const out: Record<string, string> = {};
    for (const key of Object.keys(this.filters)) {
      const value = (this.filters[key] ?? '').trim();
      if (value) {
        out[key] = value;
      }
    }
    return out;
  }
}
