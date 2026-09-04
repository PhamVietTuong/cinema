import { ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CinemaServiceAgent } from 'CinemaLib';

type Dto = CinemaServiceAgent.PatronCategoryDTO;

/** Patron-category (Adult/Student/Senior/Child) pricing management scoped to a single theater. */
@Component({
  selector: 'app-theater-patron-categories',
  standalone: false,
  templateUrl: './theater-patron-categories.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterPatronCategoriesComponent implements OnInit {
  @Input({ required: true }) theaterId!: string;

  items: Dto[] = [];

  showForm = false;
  editingId: string | null = null;
  form: FormGroup;
  private readonly _formDefaults: unknown;

  confirmOpen = false;
  private _pendingDeleteId: string | null = null;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
  ) {
    this.form = this._fb.group({
      name: ['', Validators.required],
      discountPercent: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
      isActive: [true],
      description: [''],
    });
    this._formDefaults = this.form.getRawValue();
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this._svc.getPatronCategories(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 20, filters: { theaterId: this.theaterId },
    })).subscribe(r => {
      this.items = r.results ?? [];
      this._cdr.markForCheck();
    });
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
      ? this._svc.updatePatronCategory(CinemaServiceAgent.UpdatePatronCategoryRequest.fromJS({ ...v, id: this.editingId, theaterId: this.theaterId }))
      : this._svc.createPatronCategory(CinemaServiceAgent.CreatePatronCategoryRequest.fromJS({ ...v, theaterId: this.theaterId }));
    obs.subscribe(() => {
      this.load();
      this.cancelEdit();
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
      this._svc.deletePatronCategory(id).subscribe(() => this.load());
    }
  }

  cancelEdit(): void {
    this.showForm = false;
    this.editingId = null;
    this.form.reset(this._formDefaults);
  }
}
