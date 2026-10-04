import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { Observable } from 'rxjs';
import {
  DialogService, EmptyStateComponent, SharedModule, StaffServiceAgent, StatusPillComponent, incidentCategoryLabel,
  hideLoading, showException, showLoading, showSuccess,
} from 'CinemaLib';
import { OverrideFlowService } from '../../core/override-flow.service';
import { RoomSeatPickerComponent } from '../../core/room-seat-picker.component';

/**
 * One incident: details, resolve (optionally unblocking what it blocked), and the block-seat / block-room actions.
 * Blocking needs an approver or a manager PIN override; the affected upcoming tickets are listed afterwards so staff
 * can relocate them (nothing is cancelled automatically).
 */
@Component({
  selector: 'staff-incident-detail',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent, RoomSeatPickerComponent],
  template: `
<div class="ad-page">
  @if (incident; as inc) {
    <div class="ad-page-header">
      <div>
        <button class="ad-btn ad-btn--ghost" type="button" (click)="back()"><mat-icon>arrow_back</mat-icon> {{ 'opsCommon.back' | translate }}</button>
        <h1 class="ad-h1">{{ inc.title }}
          <cl-status-pill kind="incident" [value]="inc.status" />
          <cl-status-pill kind="incidentSeverity" [value]="inc.severity" />
        </h1>
        <p class="ad-sub">{{ categoryLabel(inc.category) | translate }} · {{ inc.reportedByName }} · {{ inc.creationTime | date: 'dd/MM/yyyy HH:mm' }}</p>
      </div>
    </div>

    <mat-card class="card">
      <dl class="facts">
        @if (inc.description) {
          <dt>{{ 'incidents.fields.description' | translate }}</dt>
          <dd>{{ inc.description }}</dd>
        }
        @if (inc.roomName) {
          <dt>{{ 'incidents.fields.room' | translate }}</dt>
          <dd>{{ inc.roomName }}</dd>
        }
        @if (inc.seatLabel) {
          <dt>{{ 'incidents.fields.seat' | translate }}</dt>
          <dd>{{ inc.seatLabel }}</dd>
        }
        @if (inc.blocksSeat || inc.blocksRoom) {
          <dt>{{ 'incidents.detail.blocking' | translate }}</dt>
          <dd>
            @if (inc.blocksSeat) { <span class="ad-pill ad-pill--warn">{{ 'incidents.detail.blocksSeat' | translate }}</span> }
            @if (inc.blocksRoom) { <span class="ad-pill ad-pill--warn">{{ 'incidents.detail.blocksRoom' | translate }}</span> }
          </dd>
        }
        @if (inc.resolvedAt) {
          <dt>{{ 'incidents.detail.resolution' | translate }}</dt>
          <dd>{{ inc.resolvedByName }} · {{ inc.resolvedAt | date: 'dd/MM/yyyy HH:mm' }}
            @if (inc.resolutionNote) { <br>{{ inc.resolutionNote }} }
          </dd>
        }
      </dl>
    </mat-card>

    @if (isOpen) {
      <mat-card class="card">
        <h3 class="ad-card-title">{{ 'incidents.block.title' | translate }}</h3>
        <p class="hint">{{ (overrides.isApprover() ? 'incidents.block.hintApprover' : 'incidents.block.hintOverride') | translate }}</p>
        <form [formGroup]="blockForm">
          <staff-room-seat-picker [group]="blockForm" [theaterId]="inc.theaterId!" />
        </form>
        <div class="actions">
          <button mat-stroked-button color="warn" type="button" [disabled]="busy || !blockForm.value.seatId" (click)="blockSeat()"><mat-icon>event_seat</mat-icon> {{ 'incidents.block.seat' | translate }}</button>
          <button mat-stroked-button color="warn" type="button" [disabled]="busy || !blockForm.value.roomId" (click)="blockRoom()"><mat-icon>meeting_room</mat-icon> {{ 'incidents.block.room' | translate }}</button>
        </div>
      </mat-card>

      <mat-card class="card">
        <h3 class="ad-card-title">{{ 'incidents.resolve.title' | translate }}</h3>
        @if (inc.blocksSeat || inc.blocksRoom) {
          <mat-checkbox [checked]="unblock" (change)="unblock = $event.checked">{{ 'incidents.resolve.unblock' | translate }}</mat-checkbox>
          <p class="hint">{{ 'incidents.resolve.unblockHint' | translate }}</p>
        }
        <div class="actions">
          <button mat-raised-button color="primary" type="button" [disabled]="busy" (click)="resolve()"><mat-icon>task_alt</mat-icon> {{ 'incidents.resolve.action' | translate }}</button>
        </div>
      </mat-card>
    }

    @if (blockResult; as result) {
      <mat-card class="card ad-card--pad-0">
        <div class="panel-head">
          <h3 class="ad-card-title">{{ 'incidents.affected.title' | translate }}</h3>
          <p class="hint">{{ 'incidents.affected.hint' | translate }}</p>
        </div>
        @if (result.affectedTickets?.length) {
          <div class="ad-table-wrap">
            <table class="ad-table">
              <thead>
                <tr>
                  <th>{{ 'incidents.affected.invoice' | translate }}</th>
                  <th>{{ 'incidents.affected.customer' | translate }}</th>
                  <th>{{ 'incidents.affected.movie' | translate }}</th>
                  <th>{{ 'incidents.affected.start' | translate }}</th>
                  <th>{{ 'incidents.fields.room' | translate }}</th>
                  <th>{{ 'incidents.fields.seat' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (t of result.affectedTickets; track t.invoiceId + (t.seatId ?? '')) {
                  <tr>
                    <td><strong>{{ t.invoiceCode }}</strong></td>
                    <td>{{ t.customerName }}<br><span class="hint">{{ t.customerPhone }}</span></td>
                    <td>{{ t.movieTitle }}</td>
                    <td>{{ t.startTime | date: 'dd/MM HH:mm' }}</td>
                    <td>{{ t.roomName }}</td>
                    <td>{{ t.seatLabel }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <cl-empty-state icon="verified" messageKey="incidents.affected.none" />
        }
      </mat-card>
    }
  } @else if (notFound) {
    <mat-card class="ad-card--pad-0"><cl-empty-state icon="search_off" messageKey="incidents.detail.notFound" /></mat-card>
  }
</div>
`,
  styles: [`
    .card { padding: 16px; margin-bottom: 16px; }
    .facts { display: grid; grid-template-columns: max-content 1fr; gap: 6px 16px; margin: 0; }
    .facts dt { color: var(--ml-muted); }
    .facts dd { margin: 0; display: flex; gap: 6px; flex-wrap: wrap; }
    .hint { color: var(--ml-muted); font-size: 13px; margin: 4px 0 12px; }
    .actions { display: flex; gap: 12px; flex-wrap: wrap; margin-top: 12px; }
    .panel-head { padding: 16px 16px 0; }
  `],
})
export class IncidentDetailComponent implements OnInit {
  incident: StaffServiceAgent.IncidentDTO | null = null;
  blockResult: StaffServiceAgent.BlockResultDTO | null = null;
  blockForm: FormGroup;
  unblock = false;
  busy = false;
  notFound = false;

  readonly overrides = inject(OverrideFlowService);
  readonly categoryLabel = incidentCategoryLabel;

  private readonly _ops = inject(StaffServiceAgent.OperationsHttpService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _store = inject(Store);
  private readonly _dialogs = inject(DialogService);
  private readonly _translate = inject(TranslateService);
  private readonly _cd = inject(ChangeDetectorRef);

  constructor(fb: FormBuilder) {
    this.blockForm = fb.group({ roomId: [''], seatId: [''] });
  }

  get isOpen(): boolean {
    return this.incident?.status === StaffServiceAgent.IncidentStatus.Open;
  }

  ngOnInit(): void {
    this._route.paramMap.subscribe(params => {
      this._load(params.get('id') ?? '', true);
    });
  }

  back(): void {
    this._router.navigate(['/incidents']);
  }

  blockSeat(): void {
    const incident = this.incident;
    const seatId = this.blockForm.value.seatId as string;
    if (!incident || !seatId) {
      return;
    }
    this._block(incident, override => this._ops.blockSeat(StaffServiceAgent.BlockSeatRequest.fromJS({
      theaterId: incident.theaterId, seatId, incidentId: incident.id, override,
    })));
  }

  blockRoom(): void {
    const incident = this.incident;
    const roomId = this.blockForm.value.roomId as string;
    if (!incident || !roomId) {
      return;
    }
    this._block(incident, override => this._ops.blockRoom(StaffServiceAgent.BlockRoomRequest.fromJS({
      theaterId: incident.theaterId, roomId, incidentId: incident.id, override,
    })));
  }

  resolve(): void {
    const incident = this.incident;
    if (!incident) {
      return;
    }
    this._dialogs.openReasonDialog({
      titleKey: 'incidents.resolve.title',
      confirmKey: 'incidents.resolve.action',
      note: { labelKey: 'incidents.resolve.note' },
    }).afterClosed().subscribe(result => {
      if (!result) {
        return;
      }
      const unblock = this.unblock && !!(incident.blocksSeat || incident.blocksRoom);
      const call = (override?: StaffServiceAgent.ManagerOverrideDTO) => this._ops.resolveIncident(StaffServiceAgent.ResolveIncidentRequest.fromJS({
        incidentId: incident.id, resolutionNote: result.note || undefined, unblock, override,
      }));
      // Unblocking re-opens seats for sale, so it needs an approver (or PIN); a plain resolve does not.
      const request$: Observable<StaffServiceAgent.IncidentDTO> = unblock ? this.overrides.run(incident.theaterId, call) : call();
      this._run(request$, () => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('incidents.toast.resolved') }));
        this.blockResult = null;
        this._load(incident.id!, false);
      });
    });
  }

  private _block(incident: StaffServiceAgent.IncidentDTO, call: (override?: StaffServiceAgent.ManagerOverrideDTO) => Observable<StaffServiceAgent.BlockResultDTO>): void {
    this._run(this.overrides.run(incident.theaterId, call), result => {
      this.blockResult = result;
      this._store.dispatch(showSuccess({ message: this._translate.instant('incidents.toast.blocked') }));
      this._load(incident.id!, false);
    });
  }

  private _run<T>(request$: Observable<T>, onNext: (value: T) => void): void {
    this.busy = true;
    this._store.dispatch(showLoading());
    request$.subscribe({
      next: value => onNext(value),
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.busy = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  private _load(id: string, prefill: boolean): void {
    if (!id) {
      return;
    }
    this._ops.getIncident(StaffServiceAgent.GetIncidentRequest.fromJS({ incidentId: id })).subscribe({
      next: incident => {
        this.incident = incident;
        this.notFound = false;
        if (prefill) {
          this.blockForm.patchValue({ roomId: incident.roomId ?? '', seatId: incident.seatId ?? '' });
        }
        this._cd.markForCheck();
      },
      error: error => {
        this.notFound = true;
        this._store.dispatch(showException({ error }));
        this._cd.markForCheck();
      },
    });
  }
}
