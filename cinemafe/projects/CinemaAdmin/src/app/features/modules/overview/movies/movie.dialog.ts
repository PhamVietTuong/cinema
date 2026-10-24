import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { AbstractControl, FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException, CreditTypeValues } from 'CinemaLib';
import { ImageUploadService } from '../../../../shared/image-upload.service';

type Dto = CinemaServiceAgent.MovieDTO;

export interface MovieDialogData {
  movie: Dto | null;
}

/** Sentinel value for the "+ Add new person" option in a Cast & Crew row's Person select. Contains
 * no hex digits or hyphens, so it can never collide with a real Person id (a GUID). */
const NEW_PERSON = '__new_person__';

/** Create/edit form for a movie, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-movie-dialog',
  standalone: false,
  templateUrl: './movie.dialog.html',
  styleUrl: './movie.dialog.scss',
})
export class MovieDialog implements OnInit {
  readonly editingId: string | null;
  form: FormGroup;

  ageRestrictions: CinemaServiceAgent.AgeRestrictionDTO[] = [];
  movieTypes: CinemaServiceAgent.MovieTypeDTO[] = [];
  people: CinemaServiceAgent.PersonDTO[] = [];

  readonly creditTypeOptions = CreditTypeValues;
  readonly newPersonValue = NEW_PERSON;

  /** Credit rows aren't on the list-row DTO — on edit they load async via getMovie(). Save is
   * disabled until this is true, so a save started before they load can never wipe them. */
  creditsLoaded: boolean;

  uploading = false;
  uploadError = '';

  /** Per-row upload state for the Cast & Crew photo field, keyed by the row's own FormGroup instance
   * so it survives reordering (moveCreditRow splices the FormArray's controls). */
  private _rowUpload = new WeakMap<AbstractControl, { uploading: boolean; error: string }>();

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _upload: ImageUploadService,
    private _cdr: ChangeDetectorRef,
    private _dialogRef: MatDialogRef<MovieDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: MovieDialogData,
  ) {
    this.editingId = data.movie?.id ?? null;
    this.creditsLoaded = !this.editingId;
    this.form = this._fb.group({
      title: [data.movie?.title ?? '', Validators.required],
      description: [data.movie?.description ?? '', Validators.required],
      duration: [data.movie?.duration ?? 0, [Validators.required, Validators.min(1), Validators.max(600)]],
      releaseDate: [this._toDateInput(data.movie?.releaseDate), Validators.required],
      endDate: [this._toDateInput(data.movie?.endDate)],
      director: [data.movie?.director ?? ''],
      cast: [data.movie?.cast ?? ''],
      language: [data.movie?.language ?? ''],
      subtitle: [data.movie?.subtitle ?? ''],
      trailerUrl: [data.movie?.trailerUrl ?? ''],
      posterUrl: [data.movie?.posterUrl ?? ''],
      ageRestrictionId: [data.movie?.ageRestrictionId ?? '', Validators.required],
      movieTypeIds: [data.movie?.movieTypeIds ?? []],
      credits: this._fb.array([]),
    });
  }

  ngOnInit(): void {
    const wide = CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 });
    this._svc.getAgeRestrictions(wide).subscribe(r => {
      this.ageRestrictions = r.results ?? [];
      this._cdr.markForCheck();
    });
    this._svc.getMovieTypes(wide).subscribe(r => {
      this.movieTypes = r.results ?? [];
      this._cdr.markForCheck();
    });
    this._svc.getPeople(wide).subscribe(r => {
      this.people = r.results ?? [];
      this._cdr.markForCheck();
    });

    if (this.editingId) {
      this._svc.getMovie(this.editingId).subscribe({
        next: movie => {
          // An existing credit's person might not be in the first 200 people — merge it in so the
          // dropdown shows them selected instead of blank.
          for (const c of movie.credits ?? []) {
            if (c.personId && !this.people.some(p => p.id === c.personId)) {
              this.people = [...this.people, CinemaServiceAgent.PersonDTO.fromJS({ id: c.personId, name: c.personName, photoUrl: c.personPhotoUrl })];
            }
          }
          for (const c of movie.credits ?? []) {
            this.creditRows.push(this._creditRow({
              personId: c.personId ?? NEW_PERSON,
              personName: c.personName ?? '',
              personPhotoUrl: c.personPhotoUrl ?? '',
              creditType: c.creditType ?? CinemaServiceAgent.CreditType.Actor,
              characterName: c.characterName ?? '',
            }));
          }
          this.creditsLoaded = true;
          this._cdr.markForCheck();
        },
        error: () => {
          // Leave creditsLoaded false: save() returns early while it's false (and the Save button
          // stays disabled), so a failed load here blocks saving any edit to the movie rather than
          // risking a save that would wipe out credits this dialog never actually fetched.
        },
      });
    }
  }

  get creditRows(): FormArray { return this.form.get('credits') as FormArray; }

  addCreditRow(): void {
    this.creditRows.push(this._creditRow({
      personId: NEW_PERSON, personName: '', personPhotoUrl: '',
      creditType: CinemaServiceAgent.CreditType.Actor, characterName: '',
    }));
  }

  removeCreditRow(index: number): void {
    this.creditRows.removeAt(index);
  }

  moveCreditRow(index: number, delta: -1 | 1): void {
    const target = index + delta;
    if (target < 0 || target >= this.creditRows.length) {
      return;
    }
    const rows = this.creditRows.controls;
    const [moved] = rows.splice(index, 1);
    rows.splice(target, 0, moved);
    this.creditRows.updateValueAndValidity();
  }

  isActor(index: number): boolean {
    return this.creditRows.at(index).value.creditType === CinemaServiceAgent.CreditType.Actor;
  }

  /** Picking a person (including "+ Add new person") replaces whatever Name/Photo this row had with
   * that person's own — the row then edits whichever person is currently selected. Bound to
   * (selectionChange) rather than a value subscription so it only fires on an actual user pick, not
   * while rows are being built from a loaded movie. */
  onPersonPicked(row: FormGroup): void {
    const personId = row.value.personId;
    if (personId === NEW_PERSON) {
      row.patchValue({ personName: '', personPhotoUrl: '' });
      return;
    }
    // Prefer another row already crediting this same person (e.g. Director + Actor on one movie):
    // it may carry an edit the admin just typed, which the `people` list snapshot wouldn't have yet.
    // The backend rejects two rows for the same person with different Name/PhotoUrl in one save, so
    // rows for the same person must always agree.
    const sibling = this._siblingRow(row, personId);
    if (sibling) {
      row.patchValue({ personName: sibling.value.personName, personPhotoUrl: sibling.value.personPhotoUrl });
      return;
    }
    const person = this.people.find(p => p.id === personId);
    row.patchValue({ personName: person?.name ?? '', personPhotoUrl: person?.photoUrl ?? '' });
  }

  private _siblingRow(row: FormGroup, personId: string): FormGroup | undefined {
    return this.creditRows.controls.find(c => c !== row && (c as FormGroup).value.personId === personId) as FormGroup | undefined;
  }

  /** Keeps every row crediting the same person in sync as the admin edits Name/Photo in any one of
   * them, so two rows for the same person (e.g. Director + Actor) never disagree at save time — the
   * backend rejects that as a conflicting edit. Also updates the `people` list so the dropdown's own
   * label reflects the live edit. */
  private _syncSiblingRows(row: FormGroup, field: 'personName' | 'personPhotoUrl', value: string): void {
    const personId = row.value.personId;
    if (!personId || personId === NEW_PERSON) {
      return;
    }
    for (const control of this.creditRows.controls) {
      if (control === row) {
        continue;
      }
      const sibling = control as FormGroup;
      if (sibling.value.personId === personId && sibling.value[field] !== value) {
        sibling.get(field)!.setValue(value, { emitEvent: false });
      }
    }
    const person = this.people.find(p => p.id === personId);
    if (person) {
      if (field === 'personName') {
        person.name = value;
      } else {
        person.photoUrl = value;
      }
    }
    this._cdr.markForCheck();
  }

  rowUploading(row: AbstractControl): boolean {
    return this._rowUpload.get(row)?.uploading ?? false;
  }

  rowUploadError(row: AbstractControl): string {
    return this._rowUpload.get(row)?.error ?? '';
  }

  onPickCreditPhoto(event: Event, row: FormGroup): void {
    this._uploadImage(
      event,
      url => row.patchValue({ personPhotoUrl: url }),
      (uploading, error) => this._rowUpload.set(row, { uploading, error }),
    );
  }

  private _creditRow(init: {
    personId: string; personName: string; personPhotoUrl: string;
    creditType: CinemaServiceAgent.CreditType; characterName: string;
  }): FormGroup {
    const group = this._fb.group({
      personId: [init.personId, Validators.required],
      personName: [init.personName, [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
      personPhotoUrl: [init.personPhotoUrl, Validators.maxLength(500)],
      creditType: [init.creditType],
      characterName: [init.characterName],
    });
    group.controls['personName'].valueChanges.subscribe(v => this._syncSiblingRows(group, 'personName', v ?? ''));
    group.controls['personPhotoUrl'].valueChanges.subscribe(v => this._syncSiblingRows(group, 'personPhotoUrl', v ?? ''));
    return group;
  }

  /** Formats a Date to the `yyyy-MM-dd` value an <input type="date"> requires. */
  private _toDateInput(d?: Date): string {
    if (!d) {
      return '';
    }
    const dt = new Date(d);
    const pad = (n: number) => `${n}`.padStart(2, '0');
    return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}`;
  }

  movieTypeName(id: string): string {
    return this.movieTypes.find(t => t.id === id)?.name ?? '';
  }

  removeMovieType(id: string, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    const control = this.form.controls['movieTypeIds'];
    control.setValue((control.value as string[]).filter(t => t !== id));
    control.markAsDirty();
  }

  onPickImage(event: Event): void {
    this._uploadImage(
      event,
      url => this.form.patchValue({ posterUrl: url }),
      (uploading, error) => { this.uploading = uploading; this.uploadError = error; },
    );
  }

  private _uploadImage(event: Event, onUrl: (url: string) => void, setState: (uploading: boolean, error: string) => void): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) {
      return;
    }
    setState(true, '');
    this._upload.upload(file).subscribe({
      next: url => {
        onUrl(url);
        setState(false, '');
        this._cdr.markForCheck();
      },
      error: () => {
        setState(false, 'Tải ảnh thất bại.');
        this._cdr.markForCheck();
      },
    });
  }

  save(): void {
    if (!this.form.valid || !this.creditsLoaded) {
      this.form.markAllAsTouched();
      return;
    }
    const { credits: creditRowValues, ...v } = this.form.value;
    const payload: Record<string, unknown> = { ...v, endDate: v.endDate || undefined };
    // Reached only once creditsLoaded is true (either a new movie with none yet, or an existing
    // movie whose credits have finished loading), so the current rows are always safe to send.
    payload['credits'] = creditRowValues.map((r: any, i: number) => ({
      personId: r.personId === NEW_PERSON ? undefined : r.personId,
      personName: r.personName.trim(),
      personPhotoUrl: r.personPhotoUrl?.trim() || undefined,
      creditType: r.creditType,
      characterName: r.creditType === CinemaServiceAgent.CreditType.Actor ? (r.characterName?.trim() || undefined) : undefined,
      displayOrder: i,
    }));
    const obs = this.editingId
      ? this._svc.updateMovie(CinemaServiceAgent.UpdateMovieRequest.fromJS({ ...payload, id: this.editingId }))
      : this._svc.createMovie(CinemaServiceAgent.CreateMovieRequest.fromJS(payload));

    this._store.dispatch(showLoading());
    obs.subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}
