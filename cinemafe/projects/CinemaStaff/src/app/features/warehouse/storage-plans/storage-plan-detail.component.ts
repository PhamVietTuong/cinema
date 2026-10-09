import { ChangeDetectorRef, Component, OnDestroy, OnInit, effect, inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { Observable, of, Subscription } from 'rxjs';
import { map, switchMap, take } from 'rxjs/operators';
import {
  CinemaServiceAgent,
  DialogService,
  EmptyStateComponent,
  SharedModule,
  StatusPillComponent,
  selectIsStockApprover,
  showError,
  showException,
  showLoading,
  hideLoading,
  showSuccess,
} from 'CinemaLib';
import { TheaterContextService } from '../../../core/theater-context.service';

const Status = CinemaServiceAgent.StoragePlanStatus;
const MAX_QUANTITY = 100000;

/** Display data for a plan line (name and stock figures come from the inventory list or the loaded plan). */
interface ItemMeta {
  name: string;
  onHand: number;
  target: number;
  suggested: number;
}

/** Storage plan editor / viewer plus the whole approval workflow (submit, approve, reject, receive, cancel). */
@Component({
  selector: 'staff-storage-plan-detail',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  template: `
@if (noTheater) {
  <div class="ad-page">
    <cl-empty-state icon="theaters" messageKey="warehouse.pickTheater" hintKey="warehouse.pickTheaterHint" />
  </div>
} @else {
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <button class="ad-btn ad-btn--ghost" type="button" (click)="goBack()"><mat-icon>arrow_back</mat-icon> {{ 'storagePlans.detail.back' | translate }}</button>
      <h1 class="ad-h1">
        {{ (plan ? plan.code : ('storagePlans.detail.newTitle' | translate)) }}
        @if (plan) {
          <cl-status-pill class="status-pill" kind="storagePlan" [value]="plan.status" />
        }
      </h1>
      <p class="ad-sub">{{ 'storagePlans.detail.subtitle' | translate }}</p>
    </div>
  </div>

  @if (plan && plan.status === Status.Rejected) {
    <div class="reject-banner">
      <mat-icon>report</mat-icon>
      <div>
        <strong>{{ 'storagePlans.detail.rejectedBanner' | translate }}</strong>
        <div>{{ plan.rejectionReason }}</div>
        <small>{{ 'storagePlans.detail.rejectedHint' | translate }}</small>
      </div>
    </div>
  }

  <div class="ad-card">
    <h3 class="ad-card-title">{{ 'storagePlans.detail.infoTitle' | translate }}</h3>
    <form [formGroup]="form" class="plan-form">
      <div class="ad-field">
        <label class="ad-label">{{ 'storagePlans.list.theater' | translate }}</label>
        <input class="ad-input" [value]="theaterLabel" readonly>
      </div>
      <div class="ad-field">
        <label class="ad-label">{{ 'storagePlans.list.targetDate' | translate }}</label>
        <input class="ad-input" type="date" formControlName="targetDate" [min]="today">
        @if (showError('targetDate')) {
          <span class="ad-field-err">{{ (form.controls['targetDate'].hasError('past') ? 'storagePlans.errors.datePast' : 'common.required') | translate }}</span>
        }
      </div>
      <div class="ad-field">
        <label class="ad-label">{{ 'storagePlans.list.supplier' | translate }}</label>
        <input class="ad-input" formControlName="supplier" maxlength="200">
      </div>
      <div class="ad-field full-span">
        <label class="ad-label">{{ 'storagePlans.detail.note' | translate }}</label>
        <textarea class="ad-input" rows="2" formControlName="note" maxlength="1000"></textarea>
      </div>
    </form>

    @if (plan) {
      <dl class="meta-grid">
        <div><dt>{{ 'storagePlans.list.createdBy' | translate }}</dt><dd>{{ plan.createdByName || '-' }} · {{ plan.creationTime | serverUtc | date: 'dd/MM/yyyy HH:mm' }}</dd></div>
        @if (plan.submittedAt) {
          <div><dt>{{ 'storagePlans.detail.submittedAt' | translate }}</dt><dd>{{ plan.submittedAt | serverUtc | date: 'dd/MM/yyyy HH:mm' }}</dd></div>
        }
        @if (plan.decidedAt) {
          <div><dt>{{ (plan.status === Status.Rejected ? 'storagePlans.detail.rejectedBy' : 'storagePlans.detail.approvedBy') | translate }}</dt><dd>{{ plan.decidedByName || '-' }} · {{ plan.decidedAt | serverUtc | date: 'dd/MM/yyyy HH:mm' }}</dd></div>
        }
        @if (plan.receivedAt) {
          <div><dt>{{ 'storagePlans.detail.receivedBy' | translate }}</dt><dd>{{ plan.receivedByName || '-' }} · {{ plan.receivedAt | serverUtc | date: 'dd/MM/yyyy HH:mm' }}</dd></div>
        }
      </dl>
    }
  </div>

  <div class="ad-card">
    <div class="items-head">
      <h3 class="ad-card-title">{{ 'storagePlans.detail.itemsTitle' | translate }}</h3>
      @if (editable) {
        <div class="items-tools">
          <select class="ad-select" [value]="''" #picker (change)="addItem(picker.value); picker.value = ''" [disabled]="!availableItems.length">
            <option value="">{{ 'storagePlans.detail.addProduct' | translate }}</option>
            @for (i of availableItems; track i.id) {
              <option [value]="i.id">{{ i.name }}</option>
            }
          </select>
          <button class="ad-btn ad-btn--ghost" type="button" (click)="addLowStock()" [disabled]="!lowStockItems.length">
            <mat-icon>playlist_add</mat-icon> {{ 'storagePlans.detail.addLowStock' | translate }}
          </button>
        </div>
      }
    </div>

    <div class="ad-table-wrap">
      <table class="ad-table items-table">
        <thead>
          <tr>
            <th>{{ 'storagePlans.detail.product' | translate }}</th>
            <th class="num">{{ 'storagePlans.detail.onHand' | translate }}</th>
            <th class="num">{{ 'storagePlans.detail.planned' | translate }}</th>
            @if (plan && plan.status === Status.Received) {
              <th class="num">{{ 'storagePlans.detail.received' | translate }}</th>
            }
            <th class="num">{{ 'storagePlans.detail.unitCost' | translate }}</th>
            <th>{{ 'storagePlans.detail.itemNote' | translate }}</th>
            @if (editable) {
              <th></th>
            }
          </tr>
        </thead>
        <tbody [formGroup]="form">
          <ng-container formArrayName="items">
            @for (g of itemGroups; track g; let i = $index) {
              <tr [formGroup]="g">
                <td>
                  <strong>{{ meta(g).name }}</strong>
                  @if (editable && meta(g).target > 0) {
                    <div class="hint">{{ 'storagePlans.detail.targetLevel' | translate }}: {{ meta(g).target }}</div>
                  }
                  @if (editable && meta(g).suggested > 0) {
                    <div class="hint">
                      {{ 'storagePlans.detail.suggested' | translate }}: {{ meta(g).suggested }}
                      <a href="javascript:void(0)" (click)="useSuggested(g)">{{ 'storagePlans.detail.useSuggested' | translate }}</a>
                    </div>
                  }
                </td>
                <td class="num">{{ meta(g).onHand }}</td>
                <td class="num">
                  <input class="ad-input qty" type="number" min="1" [max]="maxQuantity" step="1" formControlName="plannedQuantity">
                  @if (g.controls['plannedQuantity'].invalid && g.controls['plannedQuantity'].touched) {
                    <span class="ad-field-err">{{ 'storagePlans.errors.plannedRange' | translate }}</span>
                  }
                </td>
                @if (plan && plan.status === Status.Received) {
                  <td class="num">{{ receivedOf(g) }}</td>
                }
                <td class="num">
                  <input class="ad-input cost" type="number" min="0" step="any" formControlName="unitCost">
                  @if (g.controls['unitCost'].invalid && g.controls['unitCost'].touched) {
                    <span class="ad-field-err">{{ 'storagePlans.errors.costNegative' | translate }}</span>
                  }
                </td>
                <td><input class="ad-input" formControlName="note" maxlength="500"></td>
                @if (editable) {
                  <td>
                    <button mat-icon-button type="button" (click)="removeItem(i)" [matTooltip]="'common.remove' | translate"><mat-icon>delete</mat-icon></button>
                  </td>
                }
              </tr>
            }
          </ng-container>
        </tbody>
      </table>
    </div>
    @if (!itemGroups.length) {
      <cl-empty-state messageKey="storagePlans.detail.noItems" />
    }
  </div>

  <div class="action-bar">
    @if (editable) {
      <button mat-raised-button type="button" (click)="saveDraft()" [disabled]="busy">
        <mat-icon>save</mat-icon> {{ 'storagePlans.actions.saveDraft' | translate }}
      </button>
    }
    @if (canSubmit) {
      <button mat-raised-button color="primary" type="button" (click)="submitPlan()" [disabled]="busy">
        <mat-icon>send</mat-icon> {{ 'storagePlans.actions.submit' | translate }}
      </button>
    }
    @if (canDecide) {
      <button mat-raised-button color="primary" type="button" (click)="approve()" [disabled]="busy">
        <mat-icon>check_circle</mat-icon> {{ 'storagePlans.actions.approve' | translate }}
      </button>
      <button mat-raised-button color="warn" type="button" (click)="reject()" [disabled]="busy">
        <mat-icon>cancel</mat-icon> {{ 'storagePlans.actions.reject' | translate }}
      </button>
    }
    @if (plan && plan.status === Status.Approved) {
      <button mat-raised-button color="primary" type="button" (click)="receive()" [disabled]="busy">
        <mat-icon>move_to_inbox</mat-icon> {{ 'storagePlans.actions.receive' | translate }}
      </button>
    }
    @if (canCancel) {
      <button mat-raised-button type="button" (click)="cancelPlan()" [disabled]="busy">
        <mat-icon>block</mat-icon> {{ 'storagePlans.actions.cancel' | translate }}
      </button>
    }
  </div>
</div>
}
`,
  styles: [`
    .status-pill { margin-left: 8px; vertical-align: middle; }
    .plan-form { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 12px 16px; }
    .plan-form .full-span { grid-column: 1 / -1; }
    .meta-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 12px 16px; margin: 16px 0 0; }
    .meta-grid dt { font-size: 12px; color: var(--ml-muted, #777); }
    .meta-grid dd { margin: 2px 0 0; }
    .reject-banner { display: flex; gap: 12px; align-items: flex-start; padding: 12px 16px; margin-bottom: 16px; border-radius: 8px;
      background: var(--ml-danger-soft, #fde8e8); color: var(--ml-danger-ink, #9b1c1c); }
    .items-head { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; }
    .items-tools { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
    .items-table { min-width: 640px; }
    .items-table .num { text-align: right; }
    .items-table .qty { width: 100px; text-align: right; }
    .items-table .cost { width: 120px; text-align: right; }
    .hint { font-size: 12px; color: var(--ml-muted, #777); }
    .action-bar { display: flex; gap: 8px; flex-wrap: wrap; margin: 16px 0; }
    @media (max-width: 600px) {
      .action-bar button { flex: 1 1 100%; }
    }
  `],
})
export class StoragePlanDetailComponent implements OnInit, OnDestroy {
  readonly Status = Status;
  readonly maxQuantity = MAX_QUANTITY;

  /** The theater the staff app is scoped to (own theater, or the one an Admin picked in the topbar). */
  private readonly _theaterContext = inject(TheaterContextService);

  planId = 'new';
  plan: CinemaServiceAgent.StoragePlanDTO | null = null;
  inventory: CinemaServiceAgent.InventoryItemDTO[] = [];
  form: FormGroup;
  today = this._toInputDate(new Date());
  busy = false;
  isApprover = false;

  private readonly _meta = new Map<string, ItemMeta>();
  private readonly _receivedByItem = new Map<string, number | undefined>();
  private _routeSub?: Subscription;
  /** False until the route param is read, so the theater effect doesn't load stock for a plan that is about to be opened. */
  private _routeResolved = false;

  constructor(
    private _route: ActivatedRoute,
    private _router: Router,
    private _fb: FormBuilder,
    private _cinema: CinemaServiceAgent.HttpService,
    private _store: Store<any>,
    private _dialogService: DialogService,
    private _cd: ChangeDetectorRef,
    private _translate: TranslateService,
  ) {
    this.form = this._fb.group({
      theaterId: ['', Validators.required],
      targetDate: [this.today, [Validators.required, this._notPast]],
      supplier: [''],
      note: [''],
      items: this._fb.array([]),
    });

    // A new plan follows the topbar theater: an Admin switching theaters starts the plan over for that one.
    effect(() => {
      const theaterId = this._theaterContext.currentTheaterId() ?? '';
      if (this._routeResolved && this.isNew && this.form.getRawValue().theaterId !== theaterId) {
        this.itemsArray.clear();
        this.form.controls['theaterId'].setValue(theaterId);
        this._loadInventory(theaterId);
        this._cd.markForCheck();
      }
    });
  }

  /** Theater name shown read-only in the info card. */
  get theaterLabel(): string {
    return this.plan?.theaterName ?? this._theaterContext.currentTheaterName() ?? '';
  }

  /** A new plan needs a theater, and an Admin has none until they pick one in the topbar. */
  get noTheater(): boolean {
    return this.isNew && !this.form.getRawValue().theaterId;
  }

  ngOnInit(): void {
    this._store.select(selectIsStockApprover).pipe(take(1)).subscribe(v => { this.isApprover = v; });

    this._routeSub = this._route.paramMap.subscribe(params => {
      this.planId = params.get('id') ?? 'new';
      this._routeResolved = true;
      if (this.planId === 'new') {
        this._startNew();
      } else {
        this._load(this.planId);
      }
    });
  }

  ngOnDestroy(): void {
    this._routeSub?.unsubscribe();
  }

  // ---------- derived state ----------

  get isNew(): boolean {
    return this.planId === 'new';
  }

  get editable(): boolean {
    return this.isNew || this.plan?.status === Status.Draft || this.plan?.status === Status.Rejected;
  }

  get canSubmit(): boolean {
    return this.plan?.status === Status.Draft;
  }

  get canDecide(): boolean {
    return this.plan?.status === Status.Submitted && this.isApprover;
  }

  get canCancel(): boolean {
    const s = this.plan?.status;
    return s === Status.Draft || s === Status.Submitted || s === Status.Approved;
  }

  get itemsArray(): FormArray {
    return this.form.controls['items'] as FormArray;
  }

  get itemGroups(): FormGroup[] {
    return this.itemsArray.controls as FormGroup[];
  }

  get availableItems(): CinemaServiceAgent.InventoryItemDTO[] {
    const used = new Set(this.itemGroups.map(g => g.value.foodAndDrinkId as string));
    return this.inventory.filter(i => !used.has(i.id as string));
  }

  get lowStockItems(): CinemaServiceAgent.InventoryItemDTO[] {
    return this.availableItems.filter(i => i.isLowStock);
  }

  meta(g: FormGroup): ItemMeta {
    return this._meta.get(g.value.foodAndDrinkId) ?? { name: '', onHand: 0, target: 0, suggested: 0 };
  }

  receivedOf(g: FormGroup): number | string {
    return this._receivedByItem.get(g.value.foodAndDrinkId) ?? '-';
  }

  showError(control: string): boolean {
    const c = this.form.controls[control];
    return c.invalid && (c.touched || c.dirty);
  }

  // ---------- loading ----------

  private _startNew(): void {
    this.plan = null;
    this.itemsArray.clear();
    this._meta.clear();
    this._receivedByItem.clear();
    this.form.reset({ theaterId: this._theaterContext.currentTheaterId() ?? '', targetDate: this.today, supplier: '', note: '' });
    this._applyEnabledState();
    this._loadInventory(this.form.getRawValue().theaterId);
    this._cd.markForCheck();
  }

  private _load(id: string): void {
    this._store.dispatch(showLoading());
    this._cinema.getStoragePlan(id).subscribe({
      next: plan => {
        this._apply(plan);
        this._loadInventory(plan.theaterId);
      },
      error: error => {
        this._store.dispatch(showException({ error }));
        this._router.navigate(['/storage-plans']);
      },
    }).add(() => {
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  private _apply(plan: CinemaServiceAgent.StoragePlanDTO): void {
    this.plan = plan;
    this.planId = plan.id as string;
    this._meta.clear();
    this._receivedByItem.clear();
    this.itemsArray.clear();
    this.form.patchValue({
      theaterId: plan.theaterId,
      targetDate: plan.targetDate ? this._toInputDate(plan.targetDate) : this.today,
      supplier: plan.supplier ?? '',
      note: plan.note ?? '',
    });
    for (const item of plan.items ?? []) {
      this._meta.set(item.foodAndDrinkId as string, {
        name: item.foodAndDrinkName ?? '',
        onHand: item.quantityOnHand ?? 0,
        target: 0,
        suggested: 0,
      });
      this._receivedByItem.set(item.foodAndDrinkId as string, item.receivedQuantity);
      this.itemsArray.push(this._createRow(item.foodAndDrinkId as string, item.plannedQuantity ?? 1, item.unitCost, item.note));
    }
    this.form.markAsPristine();
    this.form.markAsUntouched();
    this._applyEnabledState();
    this._cd.markForCheck();
  }

  private _applyEnabledState(): void {
    if (this.editable) {
      this.form.enable({ emitEvent: false });
      this.form.controls['theaterId'].disable({ emitEvent: false });
    } else {
      this.form.disable({ emitEvent: false });
    }
  }

  private _loadInventory(theaterId?: string): void {
    this.inventory = [];
    if (!theaterId) {
      this._cd.markForCheck();
      return;
    }
    this._cinema.getInventory(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 200, filters: { theaterId, trackedOnly: 'true' },
    })).subscribe(r => {
      this.inventory = r.results ?? [];
      for (const i of this.inventory) {
        this._setMetaFromInventory(i);
      }
      this._cd.markForCheck();
    });
  }

  private _setMetaFromInventory(i: CinemaServiceAgent.InventoryItemDTO): void {
    this._meta.set(i.id as string, {
      name: i.name ?? '',
      onHand: i.quantityOnHand ?? 0,
      target: i.targetStockLevel ?? 0,
      suggested: i.suggestedReorderQuantity ?? 0,
    });
  }

  // ---------- item editing ----------

  private _createRow(foodAndDrinkId: string, plannedQuantity: number, unitCost?: number | null, note?: string | null): FormGroup {
    return this._fb.group({
      foodAndDrinkId: [foodAndDrinkId],
      plannedQuantity: [plannedQuantity, [Validators.required, Validators.min(1), Validators.max(MAX_QUANTITY), Validators.pattern(/^\d+$/)]],
      unitCost: [unitCost ?? null, [Validators.min(0)]],
      note: [note ?? ''],
    });
  }

  addItem(id: string): void {
    const item = this.inventory.find(i => i.id === id);
    if (!item) {
      return;
    }
    this.itemsArray.push(this._createRow(id, Math.max(1, item.suggestedReorderQuantity ?? 1)));
    this.form.markAsDirty();
    this._cd.markForCheck();
  }

  addLowStock(): void {
    for (const item of this.lowStockItems) {
      this.itemsArray.push(this._createRow(item.id as string, Math.max(1, item.suggestedReorderQuantity ?? 1)));
    }
    this.form.markAsDirty();
    this._cd.markForCheck();
  }

  removeItem(index: number): void {
    this.itemsArray.removeAt(index);
    this.form.markAsDirty();
  }

  useSuggested(g: FormGroup): void {
    g.controls['plannedQuantity'].setValue(this.meta(g).suggested);
    g.markAsDirty();
    this.form.markAsDirty();
  }

  // ---------- actions ----------

  goBack(): void {
    this._router.navigate(['/storage-plans']);
  }

  saveDraft(): void {
    this._run(this._save$().pipe(map(plan => ({ plan, navigate: this.isNew }))), true);
  }

  submitPlan(): void {
    const save$: Observable<string> = this.form.dirty || this.isNew
      ? this._save$().pipe(map(plan => plan.id as string))
      : of(this.planId);
    this._run(save$.pipe(
      switchMap(id => this._cinema.submitStoragePlan(this._decision(id))),
      map(plan => ({ plan, navigate: false })),
    ), true);
  }

  approve(): void {
    this._run(this._cinema.approveStoragePlan(this._decision(this.planId)).pipe(map(plan => ({ plan, navigate: false }))), true);
  }

  reject(): void {
    this._dialogService.openReasonDialog({
      titleKey: 'storagePlans.reject.title',
      confirmKey: 'storagePlans.actions.reject',
      confirmColor: 'warn',
      note: { labelKey: 'storagePlans.reject.reason', placeholderKey: 'storagePlans.reject.reasonPlaceholder', required: true },
    }).afterClosed().subscribe(result => {
      if (result?.note) {
        this._run(this._cinema.rejectStoragePlan(this._decision(this.planId, result.note)).pipe(map(plan => ({ plan, navigate: false }))), true);
      }
    });
  }

  receive(): void {
    if (!this.plan) {
      return;
    }
    const planned = this._translate.instant('storagePlans.receive.planned');
    this._dialogService.openReasonDialog({
      titleKey: 'storagePlans.receive.title',
      hintKey: 'storagePlans.receive.hint',
      confirmKey: 'storagePlans.actions.receive',
      lines: {
        labelKey: 'storagePlans.receive.actual',
        errorKey: 'storagePlans.errors.receivedRange',
        items: (this.plan.items ?? []).map(item => ({
          id: item.id as string,
          label: item.foodAndDrinkName ?? '',
          hint: `${planned}: ${item.plannedQuantity}`,
          value: item.plannedQuantity ?? 0,
        })),
      },
    }).afterClosed().subscribe(result => {
      if (result?.lines) {
        const items = result.lines.map(line => CinemaServiceAgent.ReceiveStoragePlanItem.fromJS({
          storagePlanItemId: line.id,
          receivedQuantity: line.quantity,
        }));
        this._run(this._cinema.receiveStoragePlan(CinemaServiceAgent.ReceiveStoragePlanRequest.fromJS({
          id: this.planId, items,
        })).pipe(map(plan => ({ plan, navigate: false }))), true);
      }
    });
  }

  cancelPlan(): void {
    this._dialogService.openConfirmDialog({ message: 'storagePlans.confirm.cancel' }).afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this._run(this._cinema.cancelStoragePlan(this._decision(this.planId)).pipe(map(plan => ({ plan, navigate: false }))), true);
      }
    });
  }

  private _decision(id: string, reason?: string): CinemaServiceAgent.StoragePlanDecisionRequest {
    return CinemaServiceAgent.StoragePlanDecisionRequest.fromJS({ id, reason });
  }

  /** Validates the form and creates or updates the plan. */
  private _save$(): Observable<CinemaServiceAgent.StoragePlanDTO> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return this._fail('storagePlans.errors.invalidForm');
    }
    if (!this.itemGroups.length) {
      return this._fail('storagePlans.errors.noItems');
    }
    const v = this.form.getRawValue();
    const request = CinemaServiceAgent.SaveStoragePlanRequest.fromJS({
      id: this.isNew ? undefined : this.planId,
      theaterId: v.theaterId,
      targetDate: this._fromInputDate(v.targetDate),
      supplier: (v.supplier ?? '').trim() || undefined,
      note: (v.note ?? '').trim() || undefined,
      items: (v.items as { foodAndDrinkId: string; plannedQuantity: number; unitCost: number | null; note: string }[]).map(row => ({
        foodAndDrinkId: row.foodAndDrinkId,
        plannedQuantity: Number(row.plannedQuantity),
        unitCost: row.unitCost === null || (row.unitCost as unknown) === '' ? undefined : Number(row.unitCost),
        note: (row.note ?? '').trim() || undefined,
      })),
    });
    return this.isNew ? this._cinema.createStoragePlan(request) : this._cinema.updateStoragePlan(request);
  }

  private _fail(messageKey: string): Observable<never> {
    return new Observable<never>(subscriber => {
      subscriber.error({ message: messageKey, validation: true });
    });
  }

  /** Runs one workflow call, then reloads the plan and shows the success toast; errors go to the standard toast. */
  private _run(call$: Observable<{ plan: CinemaServiceAgent.StoragePlanDTO; navigate: boolean }>, reload: boolean): void {
    this.busy = true;
    this._store.dispatch(showLoading());
    call$.subscribe({
      next: ({ plan, navigate }) => {
        this._store.dispatch(showSuccess({}));
        if (navigate) {
          this._router.navigate(['/storage-plans', plan.id], { replaceUrl: true });
        } else if (reload) {
          this._load(plan.id as string);
        }
      },
      error: error => {
        if (error?.validation) {
          this._store.dispatch(showError({ message: this._translate.instant(error.message) }));
        } else {
          this._store.dispatch(showException({ error }));
        }
      },
    }).add(() => {
      this.busy = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  // ---------- date helpers ----------

  private _notPast = (control: { value: string }): { past: true } | null => {
    const value = control.value;
    return value && value < this._toInputDate(new Date()) ? { past: true } : null;
  };

  private _toInputDate(d: Date): string {
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

  /** yyyy-MM-dd -> midnight UTC, so the server's UTC-date comparison sees the chosen calendar day. */
  private _fromInputDate(value: string): Date {
    const [y, m, d] = value.split('-').map(Number);
    return new Date(Date.UTC(y, m - 1, d));
  }
}
