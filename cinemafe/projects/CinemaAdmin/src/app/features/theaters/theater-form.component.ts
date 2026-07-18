import { ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { ImageUploadService } from '../../shared/image-upload.service';
import { ModalComponent } from '../../shared/modal.component';

/** Self-contained create/edit form for a theater, shown in a scrim+modal popup. */
@Component({
  selector: 'app-theater-form',
  standalone: true,
  imports: [SharedModule, ModalComponent],
  templateUrl: './theater-form.component.html',
  styleUrl: './theater-form.component.scss',
})
export class TheaterFormComponent implements OnChanges {
  @Input() open = false;
  @Input() theater: CinemaServiceAgent.TheaterDTO | null = null;
  @Output() saved = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  uploading = false;
  uploadError = '';
  form: FormGroup;

  constructor(
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
    private _upload: ImageUploadService,
    private _translate: TranslateService,
  ) {
    this.form = this._fb.group({
      name: ['', Validators.required],
      city: ['', Validators.required],
      address: ['', Validators.required],
      phone: ['', Validators.pattern(/^(?:\+84|0)\d{9,10}$/)],
      email: ['', Validators.email],
      imageUrl: [''],
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) {
      this.uploadError = '';
      this.form.reset();
      if (this.theater) {
        const t = this.theater;
        this.form.patchValue({ name: t.name, city: t.city, address: t.address, phone: t.phone, imageUrl: t.imageUrl });
      }
    }
  }

  get editingId(): string | null {
    return this.theater?.id ?? null;
  }

  onPickImage(e: Event): void {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (!file) { return; }
    this.uploading = true; this.uploadError = '';
    this._upload.upload(file).subscribe({
      next: url => { this.form.patchValue({ imageUrl: url }); this.uploading = false; this._cdr.markForCheck(); },
      error: () => { this.uploadError = this._translate.instant('theaters.form.uploadFailed'); this.uploading = false; this._cdr.markForCheck(); },
    });
  }

  save(): void {
    if (!this.form.valid) { this.form.markAllAsTouched(); return; }
    const obs = this.editingId
      ? this._cinemaService.updateTheater(CinemaServiceAgent.UpdateTheaterRequest.fromJS({ ...this.form.value, id: this.editingId }))
      : this._cinemaService.createTheater(CinemaServiceAgent.CreateTheaterRequest.fromJS(this.form.value));
    obs.subscribe(() => { this.saved.emit(); this.closed.emit(); });
  }

  cancel(): void {
    this.closed.emit();
  }
}
