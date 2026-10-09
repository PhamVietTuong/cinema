import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { combineLatest } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import {
  EmptyStateComponent, SharedModule, CinemaServiceAgent, checklistKindLabel,
  hideLoading, showException, showLoading, showSuccess,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { canComplete, checklistProgress, missingRequired } from './checklist-rules';

/** Opens (lazily creating) and runs the pre- or post-show checklist of one showtime in one room. */
@Component({
  selector: 'staff-checklist-run',
  standalone: true,
  imports: [SharedModule, EmptyStateComponent],
  templateUrl: './checklist-run.component.html',
  styleUrl: './checklist-run.component.scss',
})
export class ChecklistRunComponent implements OnInit {
  readonly Kind = CinemaServiceAgent.ChecklistKind;

  run: CinemaServiceAgent.ChecklistRunDTO | null = null;
  kind: CinemaServiceAgent.ChecklistKind = CinemaServiceAgent.ChecklistKind.PreShow;
  noTemplate = false;
  busy = false;
  /** Draft note text per run item id (saved on blur). */
  notes: Record<string, string | undefined> = {};

  private _showTimeId = '';
  private _roomId = '';

  private readonly _ops = inject(CinemaServiceAgent.HttpService);
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
      const kind = Number(query.get('kind') ?? CinemaServiceAgent.ChecklistKind.PreShow);
      this.kind = kind === CinemaServiceAgent.ChecklistKind.PostShow ? CinemaServiceAgent.ChecklistKind.PostShow : CinemaServiceAgent.ChecklistKind.PreShow;
      this._open();
    });
  }

  back(): void {
    this._router.navigate(['/checklists']);
  }

  switchKind(kind: CinemaServiceAgent.ChecklistKind): void {
    this._router.navigate([], { relativeTo: this._route, queryParams: { kind } });
  }

  toggle(item: CinemaServiceAgent.ChecklistRunItemDTO, isDone: boolean): void {
    this._setItem(item, isDone);
  }

  onNote(itemId: string, value: string): void {
    this.notes[itemId] = value;
  }

  /** Saves the note when it differs from what the server holds. */
  saveNote(item: CinemaServiceAgent.ChecklistRunItemDTO): void {
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
    this._ops.completeChecklist(CinemaServiceAgent.CompleteChecklistRequest.fromJS({ runId: this.run.id })).subscribe({
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
    this._ops.openChecklist(CinemaServiceAgent.OpenChecklistRequest.fromJS({
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

  private _setItem(item: CinemaServiceAgent.ChecklistRunItemDTO, isDone: boolean): void {
    this.busy = true;
    this._store.dispatch(showLoading());
    const note = (this.notes[item.id!] ?? item.note ?? '').trim();
    this._ops.setChecklistItem(CinemaServiceAgent.SetChecklistItemRequest.fromJS({
      runItemId: item.id, isDone, note: note || undefined,
    })).subscribe({
      next: run => this._applyRun(run),
      error: error => {
        this._store.dispatch(showException({ error }));
        this._cd.markForCheck();
      },
    }).add(() => this._finish());
  }

  private _applyRun(run: CinemaServiceAgent.ChecklistRunDTO): void {
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
