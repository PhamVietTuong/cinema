import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { combineLatest } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import {
  EmptyStateComponent, SharedModule, StaffServiceAgent, checklistKindLabel,
  hideLoading, showException, showLoading, showSuccess,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { canComplete, checklistProgress, missingRequired } from './checklist-rules';

/** Opens (lazily creating) and runs the pre- or post-show checklist of one showtime in one room. */
@Component({
  selector: 'staff-checklist-run',
  standalone: true,
  imports: [SharedModule, EmptyStateComponent],
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <button class="ad-btn ad-btn--ghost" type="button" (click)="back()"><mat-icon>arrow_back</mat-icon> {{ 'opsCommon.back' | translate }}</button>
      <h1 class="ad-h1">{{ (run ? run.templateName : ('checklists.run.title' | translate)) }}</h1>
      <p class="ad-sub">{{ kindLabel | translate }}</p>
    </div>
    <div class="ad-toolbar">
      <button mat-stroked-button type="button" [disabled]="kind === Kind.PreShow" (click)="switchKind(Kind.PreShow)">{{ 'staffEnums.checklistKind.preShow' | translate }}</button>
      <button mat-stroked-button type="button" [disabled]="kind === Kind.PostShow" (click)="switchKind(Kind.PostShow)">{{ 'staffEnums.checklistKind.postShow' | translate }}</button>
    </div>
  </div>

  @if (noTemplate) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="fact_check" messageKey="checklists.run.noTemplate" hintKey="checklists.run.noTemplateHint" />
    </mat-card>
  }

  @if (run) {
    <mat-card class="run-card">
      <div class="progress">
        <span>{{ 'checklists.run.progress' | translate: { done: progress.done, total: progress.total } }}</span>
        <div class="bar"><span [style.width.%]="progress.percent"></span></div>
      </div>

      @if (run.completedAt) {
        <div class="completed"><mat-icon>check_circle</mat-icon> {{ 'checklists.run.completedAt' | translate: { time: (run.completedAt | date: 'dd/MM/yyyy HH:mm') } }}</div>
      }

      <ul class="items">
        @for (item of run.items; track item.id) {
          <li class="item" [class.item--done]="item.isDone">
            <mat-checkbox [checked]="!!item.isDone" [disabled]="busy || !!run.completedAt" (change)="toggle(item, $event.checked)">
              {{ item.text }}
              @if (item.isRequired) {
                <span class="req">{{ 'checklists.run.required' | translate }}</span>
              }
            </mat-checkbox>
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="note">
              <mat-label>{{ 'checklists.run.note' | translate }}</mat-label>
              <input matInput maxlength="500" [value]="notes[item.id!]" [disabled]="!!run.completedAt"
                (input)="onNote(item.id!, $any($event.target).value)" (blur)="saveNote(item)">
            </mat-form-field>
            @if (item.isDone && item.doneByName) {
              <span class="who">{{ item.doneByName }} · {{ item.doneAt | date: 'HH:mm' }}</span>
            }
          </li>
        }
      </ul>

      @if (!run.completedAt) {
        <div class="footer">
          @if (missing > 0) {
            <span class="hint">{{ 'checklists.run.missing' | translate: { count: missing } }}</span>
          }
          <button mat-raised-button color="primary" type="button" [disabled]="busy || !completable" (click)="complete()">
            <mat-icon>done_all</mat-icon> {{ 'checklists.run.complete' | translate }}
          </button>
        </div>
      }
    </mat-card>
  }
</div>
`,
  styles: [`
    .run-card { padding: 16px; }
    .progress { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; }
    .bar { flex: 1; height: 6px; background: var(--ml-panel-3, rgba(128, 128, 128, 0.2)); border-radius: 3px; overflow: hidden; }
    .bar span { display: block; height: 100%; background: var(--ml-action-strong); }
    .completed { display: flex; align-items: center; gap: 8px; color: var(--ml-success-ink); margin-bottom: 8px; }
    .items { list-style: none; margin: 0; padding: 0; }
    .item { display: grid; grid-template-columns: minmax(220px, 1fr) minmax(200px, 1fr) auto; gap: 12px; align-items: center; padding: 8px 0; border-bottom: 1px solid var(--ml-line, rgba(128, 128, 128, 0.2)); }
    .item--done { opacity: 0.85; }
    .req { margin-left: 6px; font-size: 11px; color: var(--ml-danger-ink); text-transform: uppercase; }
    .who { color: var(--ml-muted); font-size: 12px; }
    .footer { display: flex; justify-content: flex-end; align-items: center; gap: 16px; margin-top: 16px; }
    .hint { color: var(--ml-muted); }
    @media (max-width: 720px) { .item { grid-template-columns: 1fr; } }
  `],
})
export class ChecklistRunComponent implements OnInit {
  readonly Kind = StaffServiceAgent.ChecklistKind;

  run: StaffServiceAgent.ChecklistRunDTO | null = null;
  kind: StaffServiceAgent.ChecklistKind = StaffServiceAgent.ChecklistKind.PreShow;
  noTemplate = false;
  busy = false;
  /** Draft note text per run item id (saved on blur). */
  notes: Record<string, string | undefined> = {};

  private _showTimeId = '';
  private _roomId = '';

  private readonly _ops = inject(StaffServiceAgent.OperationsHttpService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _store = inject(Store);
  private readonly _translate = inject(TranslateService);
  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _cd = inject(ChangeDetectorRef);

  get kindLabel(): string {
    return checklistKindLabel(this.kind);
  }

  get progress() {
    return checklistProgress(this.run?.items ?? []);
  }

  get missing(): number {
    return missingRequired(this.run?.items ?? []).length;
  }

  get completable(): boolean {
    return canComplete(this.run?.items ?? []);
  }

  ngOnInit(): void {
    combineLatest([this._route.paramMap, this._route.queryParamMap]).subscribe(([params, query]) => {
      this._showTimeId = params.get('showTimeId') ?? '';
      this._roomId = params.get('roomId') ?? '';
      const kind = Number(query.get('kind') ?? StaffServiceAgent.ChecklistKind.PreShow);
      this.kind = kind === StaffServiceAgent.ChecklistKind.PostShow ? StaffServiceAgent.ChecklistKind.PostShow : StaffServiceAgent.ChecklistKind.PreShow;
      this._open();
    });
  }

  back(): void {
    this._router.navigate(['/checklists']);
  }

  switchKind(kind: StaffServiceAgent.ChecklistKind): void {
    this._router.navigate([], { relativeTo: this._route, queryParams: { kind } });
  }

  toggle(item: StaffServiceAgent.ChecklistRunItemDTO, isDone: boolean): void {
    this._setItem(item, isDone);
  }

  onNote(itemId: string, value: string): void {
    this.notes[itemId] = value;
  }

  /** Saves the note when it differs from what the server holds. */
  saveNote(item: StaffServiceAgent.ChecklistRunItemDTO): void {
    const draft = (this.notes[item.id!] ?? '').trim();
    if (draft === (item.note ?? '')) {
      return;
    }
    this._setItem(item, !!item.isDone);
  }

  complete(): void {
    if (!this.run || !this.completable) {
      return;
    }
    this.busy = true;
    this._store.dispatch(showLoading());
    this._ops.completeChecklist(StaffServiceAgent.CompleteChecklistRequest.fromJS({ runId: this.run.id })).subscribe({
      next: run => {
        this._applyRun(run);
        this._store.dispatch(showSuccess({ message: this._translate.instant('checklists.toast.completed') }));
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._finish());
  }

  private _open(): void {
    if (!this._showTimeId || !this._roomId) {
      return;
    }
    this.run = null;
    this.noTemplate = false;
    this.busy = true;
    this._store.dispatch(showLoading());
    this._ops.openChecklist(StaffServiceAgent.OpenChecklistRequest.fromJS({
      theaterId: this._theaterContext.currentTheaterId() ?? undefined,
      showTimeId: this._showTimeId,
      roomId: this._roomId,
      kind: this.kind,
    })).subscribe({
      next: run => this._applyRun(run),
      error: error => {
        this.noTemplate = true;
        this._store.dispatch(showException({ error }));
      },
    }).add(() => this._finish());
  }

  private _setItem(item: StaffServiceAgent.ChecklistRunItemDTO, isDone: boolean): void {
    this.busy = true;
    this._store.dispatch(showLoading());
    const note = (this.notes[item.id!] ?? item.note ?? '').trim();
    this._ops.setChecklistItem(StaffServiceAgent.SetChecklistItemRequest.fromJS({
      runItemId: item.id, isDone, note: note || undefined,
    })).subscribe({
      next: run => this._applyRun(run),
      error: error => {
        this._store.dispatch(showException({ error }));
        this._cd.markForCheck();
      },
    }).add(() => this._finish());
  }

  private _applyRun(run: StaffServiceAgent.ChecklistRunDTO): void {
    this.run = run;
    this.notes = {};
    for (const item of run.items ?? []) {
      this.notes[item.id!] = item.note ?? '';
    }
    this._cd.markForCheck();
  }

  private _finish(): void {
    this.busy = false;
    this._store.dispatch(hideLoading());
    this._cd.markForCheck();
  }
}
