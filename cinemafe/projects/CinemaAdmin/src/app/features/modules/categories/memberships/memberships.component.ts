import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';
import { CinemaServiceAgent, ToastService, apiErrorMessage } from 'CinemaLib';

type Dto = CinemaServiceAgent.MemberShipDTO;

@Component({
  selector: 'app-memberships',
  standalone: false,
  templateUrl: './memberships.component.html',
})
export class MembershipsManagementComponent implements OnInit, OnDestroy {
  items: Dto[] = [];
  totalCount = 0;
  pageIndex = 1;
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 20, 50];
  filters: Record<string, string> = {};

  showForm = false;
  editingId: string | null = null;
  form: FormGroup;
  private readonly _formDefaults: unknown;

  confirmOpen = false;
  private _pendingDeleteId: string | null = null;

  private readonly _filter$ = new Subject<void>();
  private readonly _destroy$ = new Subject<void>();

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
    private _toast: ToastService,
    private _translate: TranslateService,
  ) {
    this.form = this._fb.group({
      name: ['', Validators.required],
      minPoints: [0, [Validators.required, Validators.min(0)]],
      maxPoints: [0, [Validators.required, Validators.min(0)]],
      discountPercent: [0, [Validators.required, Validators.min(0)]],
    });
    this._formDefaults = this.form.getRawValue();
  }

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
    this._svc.getMemberShips(CinemaServiceAgent.PagingSearchDTO.fromJS({
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

  openCreate(): void {
    this.editingId = null;
    this.form.reset(this._formDefaults);
    this.showForm = true;
  }

  edit(item: Dto): void {
    this.editingId = item.id ?? null;
    this.form.reset(this._formDefaults);
    this.form.patchValue(item);
    this.showForm = true;
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateMemberShip(CinemaServiceAgent.UpdateMemberShipRequest.fromJS({ ...v, id: this.editingId }))
      : this._svc.createMemberShip(CinemaServiceAgent.CreateMemberShipRequest.fromJS(v));
    obs.subscribe({
      next: () => {
        this.load();
        this.cancelEdit();
      },
      error: e => { this._toastError(e, 'common.saveFailed'); },
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
      this._svc.deleteMemberShip(id).subscribe({
        next: () => this.load(),
        error: e => { this._toastError(e, 'common.deleteFailed'); },
      });
    }
  }

  cancelEdit(): void {
    this.showForm = false;
    this.editingId = null;
    this.form.reset(this._formDefaults);
  }

  private _toastError(err: unknown, fallbackKey: string): void {
    this._toast.error(apiErrorMessage(err, this._translate.instant(fallbackKey)));
    this._cdr.markForCheck();
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
