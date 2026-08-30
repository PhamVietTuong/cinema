import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';
import { PaymentServiceAgent, ToastService } from 'CinemaLib';

type Dto = PaymentServiceAgent.GiftCardDTO;

@Component({
  selector: 'app-gift-cards',
  standalone: false,
  templateUrl: './gift-cards.component.html',
})
export class GiftCardsManagementComponent implements OnInit, OnDestroy {
  items: Dto[] = [];
  totalCount = 0;
  pageIndex = 1;
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 20, 50];
  filters: Record<string, string> = {};

  showForm = false;
  form: FormGroup;
  private readonly _formDefaults: unknown;

  private readonly _filter$ = new Subject<void>();
  private readonly _destroy$ = new Subject<void>();

  constructor(
    private _svc: PaymentServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
    private _toast: ToastService,
    private _translate: TranslateService,
  ) {
    this.form = this._fb.group({
      amount: [null, [Validators.required, Validators.min(1)]],
      expiresAt: [''],
      issuedToEmail: [''],
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
    this._svc.getGiftCards(PaymentServiceAgent.PagingSearchDTO.fromJS({
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
    this.form.reset(this._formDefaults);
    this.showForm = true;
  }

  cancelEdit(): void {
    this.showForm = false;
    this.form.reset(this._formDefaults);
  }

  /** Issue a new gift card, then reload and toast the generated code. */
  issue(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const request = PaymentServiceAgent.IssueGiftCardRequest.fromJS({
      amount: v.amount,
      expiresAt: v.expiresAt || undefined,
      issuedToEmail: v.issuedToEmail?.trim() || undefined,
    });
    this._svc.issueGiftCard(request).subscribe({
      next: card => {
        this._toast.success(this._translate.instant('giftCards.issued', { code: card.code }));
        this.cancelEdit();
        this.load();
      },
      error: e => {
        this._toast.error(this._err(e, this._translate.instant('giftCards.issueFailed')));
      },
    });
  }

  /** Enable or disable a gift card, then reload the current page. */
  toggleActive(x: Dto): void {
    if (!x.id) {
      return;
    }
    const active = !x.isActive;
    this._svc.setGiftCardActive(PaymentServiceAgent.SetGiftCardActiveRequest.fromJS({ id: x.id, active }))
      .subscribe({
        next: () => {
          this._toast.success(this._translate.instant(active ? 'giftCards.enabled' : 'giftCards.disabled'));
          this.load();
        },
        error: e => {
          this._toast.error(this._err(e, this._translate.instant('giftCards.updateFailed')));
        },
      });
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
