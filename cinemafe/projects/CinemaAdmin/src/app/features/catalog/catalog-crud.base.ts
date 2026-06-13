import { ChangeDetectorRef, Directive, OnInit, OnDestroy, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Observable, Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { ImageUploadService } from '../../shared/image-upload.service';

/** Shape of the paged result returned by every catalog list endpoint. */
export interface CatalogPage<T> {
  results?: T[] | undefined;
  totalCount?: number | undefined;
}

/**
 * Shared list + create/edit/delete logic for the simple "catalog" lookup pages.
 * Listing is server-side: the page index, page size and per-column filters are
 * sent to the backend on every change. Subclasses supply the form shape and the
 * four service calls; the table/form markup lives in each subclass's template.
 */
@Directive()
export abstract class CatalogCrudBase<T extends { id?: string }> implements OnInit, OnDestroy {
  protected _fb = inject(FormBuilder);
  protected readonly _cdr = inject(ChangeDetectorRef);
  private readonly _upload = inject(ImageUploadService);

  uploading = false;
  uploadError = '';

  items: T[] = [];
  totalCount = 0;
  pageIndex = 1; // 1-based
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 20, 50];

  /** Per-column filter values, keyed by entity property name (sent to the server). */
  filters: Record<string, string> = {};

  showForm = false;
  editingId: string | null = null;
  form: FormGroup = this.buildForm();

  private readonly _filter$ = new Subject<void>();
  private readonly _destroy$ = new Subject<void>();

  // ── Subclass hooks ──────────────────────────────────────────────────────────
  abstract buildForm(): FormGroup;
  abstract fetch(pageIndex: number, pageSize: number, filters: Record<string, string>): Observable<CatalogPage<T>>;
  abstract create(value: any): Observable<unknown>;
  abstract update(value: any, id: string): Observable<unknown>;
  abstract remove(id: string): Observable<unknown>;
  /** Maps a row to the form's value when editing (override to format dates etc.). */
  protected toFormValue(item: T): any {
    return item;
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
    this.fetch(this.pageIndex, this.pageSize, this._activeFilters()).subscribe({
      next: r => {
        this.items = r.results ?? [];
        this.totalCount = r.totalCount ?? 0;
        // Zoneless app: notify change detection so the loaded rows render.
        this._cdr.markForCheck();
      },
    });
  }

  /** Fired (debounced) by the per-column filter inputs. */
  onFilterChange(): void {
    this._filter$.next();
  }

  // ── Pagination ──────────────────────────────────────────────────────────────
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

  // ── Create / edit / delete ────────────────────────────────────────────────────
  openCreate(): void {
    this.cancelEdit();
    this.showForm = true;
  }

  edit(item: T): void {
    this.editingId = item.id ?? null;
    this.showForm = true;
    this.form.reset();
    this.form.patchValue(this.toFormValue(item));
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const obs = this.editingId
      ? this.update(this.form.value, this.editingId)
      : this.create(this.form.value);
    obs.subscribe({
      next: () => {
        this.load();
        this.cancelEdit();
      },
    });
  }

  delete(id?: string): void {
    if (id && confirm('Bạn có chắc muốn xóa mục này?')) {
      this.remove(id).subscribe({ next: () => this.load() });
    }
  }

  cancelEdit(): void {
    this.showForm = false;
    this.editingId = null;
    this.form.reset();
    this.uploadError = '';
  }

  /** Uploads the picked image and writes its URL into the given form control. */
  onPickImage(event: Event, controlName: string): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) {
      return;
    }
    this.uploading = true;
    this.uploadError = '';
    this._upload.upload(file).subscribe({
      next: url => {
        this.form.patchValue({ [controlName]: url });
        this.uploading = false;
        this._cdr.markForCheck();
      },
      error: () => {
        this.uploadError = 'Tải ảnh thất bại.';
        this.uploading = false;
        this._cdr.markForCheck();
      },
    });
  }

  /** Drops empty filter values so only active filters reach the server. */
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
