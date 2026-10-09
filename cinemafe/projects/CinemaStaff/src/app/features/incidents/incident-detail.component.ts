import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { Observable } from 'rxjs';
import {
  DialogService, EmptyStateComponent, SharedModule, CinemaServiceAgent, StatusPillComponent, incidentCategoryLabel,
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
  templateUrl: './incident-detail.component.html',
  styleUrl: './incident-detail.component.scss',
})
export class IncidentDetailComponent implements OnInit {
  incident: CinemaServiceAgent.IncidentDTO | null = null;
  blockResult: CinemaServiceAgent.BlockResultDTO | null = null;
  blockForm: FormGroup;
  unblock = false;
  busy = false;
  notFound = false;

  readonly overrides = inject(OverrideFlowService);
  readonly categoryLabel = incidentCategoryLabel;

  private readonly _ops = inject(CinemaServiceAgent.HttpService);
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
    return this.incident?.status === CinemaServiceAgent.IncidentStatus.Open;
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
    this._block(incident, override => this._ops.blockSeat(CinemaServiceAgent.BlockSeatRequest.fromJS({
      theaterId: incident.theaterId, seatId, incidentId: incident.id, override,
    })));
  }

  blockRoom(): void {
    const incident = this.incident;
    const roomId = this.blockForm.value.roomId as string;
    if (!incident || !roomId) {
      return;
    }
    this._block(incident, override => this._ops.blockRoom(CinemaServiceAgent.BlockRoomRequest.fromJS({
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
      const call = (override?: CinemaServiceAgent.ManagerOverrideDTO) => this._ops.resolveIncident(CinemaServiceAgent.ResolveIncidentRequest.fromJS({
        incidentId: incident.id, resolutionNote: result.note || undefined, unblock, override,
      }));
      // Unblocking re-opens seats for sale, so it needs an approver (or PIN); a plain resolve does not.
      const request$: Observable<CinemaServiceAgent.IncidentDTO> = unblock ? this.overrides.run(incident.theaterId, call) : call();
      this._run(request$, () => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('incidents.toast.resolved') }));
        this.blockResult = null;
        this._load(incident.id!, false);
      });
    });
  }

  private _block(incident: CinemaServiceAgent.IncidentDTO, call: (override?: CinemaServiceAgent.ManagerOverrideDTO) => Observable<CinemaServiceAgent.BlockResultDTO>): void {
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
    this._ops.getIncident(CinemaServiceAgent.GetIncidentRequest.fromJS({ incidentId: id })).subscribe({
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
