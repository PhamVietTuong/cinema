import { ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { IdentityServiceAgent, CinemaServiceAgent } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';

const PHONE_PATTERN = /^(?:\+84|0)\d{9,10}$/;

/** Self-contained create/edit form for a user account, shown in a scrim+modal popup. */
@Component({
  selector: 'app-user-form',
  standalone: false,
  templateUrl: './user-form.component.html',
  styleUrl: './user-form.component.scss',
})
export class UserFormComponent implements OnInit, OnChanges {
  @Input() open = false;
  @Input() user: IdentityServiceAgent.UserDTO | null = null;
  @Output() saved = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  readonly UserStatus = IdentityServiceAgent.UserStatus;
  readonly statuses: { v: IdentityServiceAgent.UserStatus; label: string }[];
  userTypes: CinemaServiceAgent.UserTypeDTO[] = [];
  form: FormGroup;
  errorMsg = '';

  constructor(
    private _identity: IdentityServiceAgent.HttpService,
    private _cinema: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
    private _translate: TranslateService,
  ) {
    this.statuses = [
      { v: IdentityServiceAgent.UserStatus.Active, label: this._translate.instant('users.status.active') },
      { v: IdentityServiceAgent.UserStatus.Inactive, label: this._translate.instant('users.status.locked') },
      { v: IdentityServiceAgent.UserStatus.Banned, label: this._translate.instant('users.status.banned') },
    ];
    this.form = this._fb.group({
      name: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      phone: ['', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      userTypeId: ['', Validators.required],
      status: [IdentityServiceAgent.UserStatus.Active, Validators.required],
    });
  }

  get editingId(): string | null { return this.user?.id ?? null; }

  ngOnInit(): void {
    this._cinema.getUserTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.userTypes = r.results ?? []; this._cdr.markForCheck(); });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) { this._sync(); }
  }

  private _sync(): void {
    this.errorMsg = '';
    const email = this.form.get('email')!;
    const password = this.form.get('password')!;
    if (this.user) {
      // Editing: email and password are not changed from this form.
      email.disable(); password.disable();
      this.form.reset({ status: IdentityServiceAgent.UserStatus.Active });
      this.form.patchValue({
        name: this.user.name ?? '',
        phone: this.user.phone ?? '',
        userTypeId: this.user.userTypeId ?? '',
        status: this.user.status ?? IdentityServiceAgent.UserStatus.Active,
      });
    } else {
      email.enable(); password.enable();
      this.form.reset({ status: IdentityServiceAgent.UserStatus.Active });
    }
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.errorMsg = '';
    const obs = this.editingId
      ? this._identity.updateUser(IdentityServiceAgent.UpdateUserRequest.fromJS({
          id: this.editingId, name: v.name, phone: v.phone, userTypeId: v.userTypeId, status: v.status,
        }))
      : this._identity.createUser(IdentityServiceAgent.CreateUserRequest.fromJS({
          name: v.name, email: v.email, phone: v.phone, password: v.password, userTypeId: v.userTypeId, status: v.status,
        }));
    obs.subscribe({
      next: () => { this.saved.emit(); this.closed.emit(); },
      error: e => { this.errorMsg = this._err(e); this._cdr.markForCheck(); },
    });
  }

  cancel(): void { this.closed.emit(); }

  private _err(e: any): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.message || this._translate.instant('users.form.saveFailed'));
  }
}
