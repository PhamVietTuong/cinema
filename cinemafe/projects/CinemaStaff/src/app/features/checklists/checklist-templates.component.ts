import { ChangeDetectorRef, Component, effect, inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import {
  ChecklistKindValues, EmptyStateComponent, SharedModule, CinemaServiceAgent,
  hideLoading, showException, showLoading, showSuccess,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { moveItem } from './checklist-rules';

/** Template editor for approvers: the pre-show and post-show checklists a theater's staff run for each showtime. */
@Component({
  selector: 'staff-checklist-templates',
  standalone: true,
  imports: [SharedModule, EmptyStateComponent],
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <button class="ad-btn ad-btn--ghost" type="button" (click)="back()"><mat-icon>arrow_back</mat-icon> {{ 'opsCommon.back' | translate }}</button>
      <h1 class="ad-h1">{{ 'checklists.templates.title' | translate }}</h1>
      <p class="ad-sub">{{ 'checklists.templates.subtitle' | translate }}</p>
    </div>
    @if (theaterId) {
      <div class="ad-toolbar">
        <button mat-raised-button color="primary" type="button" (click)="startNew()"><mat-icon>add</mat-icon> {{ 'checklists.templates.new' | translate }}</button>
      </div>
    }
  </div>

  @if (!theaterId) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="theaters" messageKey="opsCommon.pickTheater" hintKey="opsCommon.pickTheaterHint" />
    </mat-card>
  } @else {
    <div class="layout">
      <mat-card class="list-card">
        @for (tpl of templates; track tpl.id) {
          <button type="button" class="tpl" [class.tpl--active]="tpl.id === editingId" (click)="edit(tpl)">
            <strong>{{ tpl.name }}</strong>
            <span class="muted">{{ kindLabel(tpl.kind) | translate }} · {{ tpl.items?.length ?? 0 }} {{ 'checklists.templates.items' | translate }}</span>
            @if (!tpl.isActive) {
              <span class="muted">{{ 'checklists.templates.inactive' | translate }}</span>
            }
          </button>
        }
        @if (!templates.length) {
          <cl-empty-state messageKey="checklists.templates.empty" />
        }
      </mat-card>

      @if (form) {
        <mat-card class="form-card">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="grow">
                <mat-label>{{ 'checklists.templates.name' | translate }}</mat-label>
                <input matInput maxlength="150" formControlName="name">
                <mat-error>{{ 'common.required' | translate }}</mat-error>
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ 'checklists.templates.kind' | translate }}</mat-label>
                <mat-select formControlName="kind">
                  @for (k of kinds; track k.value) {
                    <mat-option [value]="k.value">{{ k.name | translate }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-slide-toggle formControlName="isActive">{{ 'checklists.templates.active' | translate }}</mat-slide-toggle>
            </div>

            <h3 class="ad-card-title">{{ 'checklists.templates.itemsTitle' | translate }}</h3>
            <div formArrayName="items" class="items">
              @for (item of items.controls; track item; let i = $index) {
                <div class="item" [formGroupName]="i">
                  <mat-form-field appearance="outline" subscriptSizing="dynamic" class="grow">
                    <mat-label>{{ 'checklists.templates.itemText' | translate }}</mat-label>
                    <input matInput maxlength="300" formControlName="text">
                    <mat-error>{{ 'common.required' | translate }}</mat-error>
                  </mat-form-field>
                  <mat-checkbox formControlName="isRequired">{{ 'checklists.run.required' | translate }}</mat-checkbox>
                  <button mat-icon-button type="button" [disabled]="i === 0" [attr.aria-label]="'checklists.templates.moveUp' | translate" (click)="move(i, i - 1)"><mat-icon>arrow_upward</mat-icon></button>
                  <button mat-icon-button type="button" [disabled]="i === items.length - 1" [attr.aria-label]="'checklists.templates.moveDown' | translate" (click)="move(i, i + 1)"><mat-icon>arrow_downward</mat-icon></button>
                  <button mat-icon-button type="button" [attr.aria-label]="'common.remove' | translate" (click)="removeItem(i)"><mat-icon>delete</mat-icon></button>
                </div>
              }
            </div>
            <div class="footer">
              <button mat-stroked-button type="button" (click)="addItem()"><mat-icon>add</mat-icon> {{ 'checklists.templates.addItem' | translate }}</button>
              <span class="grow"></span>
              <button mat-raised-button color="primary" type="submit">{{ 'common.save' | translate }}</button>
            </div>
          </form>
        </mat-card>
      }
    </div>
  }
</div>
`,
  styles: [`
    .layout { display: grid; grid-template-columns: 280px 1fr; gap: 16px; align-items: start; }
    .list-card { padding: 8px; display: flex; flex-direction: column; gap: 4px; }
    .tpl { text-align: left; border: 1px solid transparent; background: transparent; padding: 10px 12px; border-radius: 6px; cursor: pointer; display: flex; flex-direction: column; gap: 2px; color: inherit; }
    .tpl:hover { background: var(--ml-panel-3, rgba(128, 128, 128, 0.12)); }
    .tpl--active { border-color: var(--ml-action-strong); }
    .muted { color: var(--ml-muted); font-size: 12px; }
    .form-card { padding: 16px; }
    .row { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; margin-bottom: 8px; }
    .grow { flex: 1 1 220px; }
    .items { display: flex; flex-direction: column; gap: 8px; }
    .item { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .footer { display: flex; gap: 12px; margin-top: 16px; align-items: center; }
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } }
  `],
})
export class ChecklistTemplatesComponent {
  readonly kinds = ChecklistKindValues;

  templates: CinemaServiceAgent.ChecklistTemplateDTO[] = [];
  form: FormGroup | null = null;
  editingId: string | null = null;

  private readonly _ops = inject(CinemaServiceAgent.HttpService);
  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _router = inject(Router);
  private readonly _fb = inject(FormBuilder);
  private readonly _translate = inject(TranslateService);
  private readonly _cd = inject(ChangeDetectorRef);

  constructor() {
    effect(() => {
      this._theaterContext.currentTheaterId();
      this.form = null;
      this.editingId = null;
      this.load();
    });
  }

  get theaterId(): string {
    return this._theaterContext.currentTheaterId() ?? '';
  }

  get items(): FormArray {
    return this.form!.get('items') as FormArray;
  }

  kindLabel(kind?: CinemaServiceAgent.ChecklistKind): string {
    return this.kinds.find(k => k.value === kind)?.name ?? this.kinds[0].name;
  }

  back(): void {
    this._router.navigate(['/checklists']);
  }

  load(): void {
    const theaterId = this.theaterId;
    if (!theaterId) {
      this.templates = [];
      this._cd.markForCheck();
      return;
    }
    this._store.dispatch(showLoading());
    this._ops.getChecklistTemplates(CinemaServiceAgent.GetChecklistTemplatesRequest.fromJS({ theaterId })).subscribe({
      next: templates => {
        this.templates = templates ?? [];
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  startNew(): void {
    this.editingId = null;
    this.form = this._buildForm(null);
    this._cd.markForCheck();
  }

  edit(template: CinemaServiceAgent.ChecklistTemplateDTO): void {
    this.editingId = template.id ?? null;
    this.form = this._buildForm(template);
    this._cd.markForCheck();
  }

  addItem(): void {
    this.items.push(this._itemGroup(null));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  move(from: number, to: number): void {
    const moved = moveItem(this.items.controls, from, to);
    while (this.items.length) {
      this.items.removeAt(0);
    }
    for (const control of moved) {
      this.items.push(control);
    }
  }

  save(): void {
    if (!this.form) {
      return;
    }
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.value;
    this._store.dispatch(showLoading());
    this._ops.saveChecklistTemplate(CinemaServiceAgent.SaveChecklistTemplateRequest.fromJS({
      id: this.editingId ?? undefined,
      theaterId: this.theaterId,
      name: (value.name as string).trim(),
      kind: value.kind,
      isActive: !!value.isActive,
      items: (value.items as { id?: string; text: string; isRequired: boolean }[]).map(item => ({
        id: item.id || undefined, text: item.text.trim(), isRequired: !!item.isRequired,
      })),
    })).subscribe({
      next: saved => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('checklists.toast.templateSaved') }));
        this.editingId = saved.id ?? null;
        this.form = this._buildForm(saved);
        this.load();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  private _buildForm(template: CinemaServiceAgent.ChecklistTemplateDTO | null): FormGroup {
    return this._fb.group({
      name: [template?.name ?? '', [Validators.required, Validators.pattern(/\S/)]],
      kind: [template?.kind ?? CinemaServiceAgent.ChecklistKind.PreShow],
      isActive: [template?.isActive ?? true],
      items: this._fb.array((template?.items ?? []).map(item => this._itemGroup(item))),
    });
  }

  private _itemGroup(item: CinemaServiceAgent.ChecklistTemplateItemDTO | null): FormGroup {
    return this._fb.group({
      id: [item?.id ?? ''],
      text: [item?.text ?? '', [Validators.required, Validators.pattern(/\S/)]],
      isRequired: [item?.isRequired ?? true],
    });
  }
}
