import { ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CinemaServiceAgent } from 'CinemaLib';

type Dto = CinemaServiceAgent.SeatTypeDTO;

/** Seat-type management scoped to a single theater. */
@Component({
  selector: 'app-theater-seat-types',
  standalone: false,
  templateUrl: './theater-seat-types.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterSeatTypesComponent implements OnInit {
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
      color: ['#808080', Validators.required],
      priceMultiplier: [1, [Validators.required, Validators.min(0.1)]],
      description: [''],
    });
    this._formDefaults = this.form.getRawValue();
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 10, filters: { theaterId: this.theaterId },
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
      ? this._svc.updateSeatType(CinemaServiceAgent.UpdateSeatTypeRequest.fromJS({ ...v, id: this.editingId, theaterId: this.theaterId }))
      : this._svc.createSeatType(CinemaServiceAgent.CreateSeatTypeRequest.fromJS({ ...v, theaterId: this.theaterId }));
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
      this._svc.deleteSeatType(id).subscribe(() => this.load());
    }
  }

  cancelEdit(): void {
    this.showForm = false;
    this.editingId = null;
    this.form.reset(this._formDefaults);
  }
}
