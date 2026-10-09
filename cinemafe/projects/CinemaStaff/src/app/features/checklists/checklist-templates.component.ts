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
  templateUrl: './checklist-templates.component.html',
  styleUrl: './checklist-templates.component.scss',
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
