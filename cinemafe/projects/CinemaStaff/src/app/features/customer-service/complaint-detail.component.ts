import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { Observable } from 'rxjs';
import {
  DialogService, EmptyStateComponent, SharedModule, CinemaServiceAgent, StatusPillComponent, complaintCategoryLabel, complaintResolutionLabel,
  hideLoading, showException, showLoading, showSuccess,
} from 'CinemaLib';
import { OverrideFlowService } from '../../core/override-flow.service';
import { SensitiveCallService } from '../../core/sensitive-call.service';
import { TheaterContextService } from '../../core/theater-context.service';
import { ComplaintDialogComponent, ComplaintDialogData } from './complaint.dialog';
import { ComplaintAction, complaintActions, isComplaintClosed, resolutionNeedsPin } from './customer-service.logic';
import { ResolveComplaintChoice, ResolveComplaintDialogComponent, ResolveComplaintDialogData } from './resolve-complaint.dialog';

/**
 * One complaint with its workflow: start review (assigns to me), reject with a reason, resolve with a compensation
 * (refund / gift card / points need an approver or a manager PIN, an apology does not) and edit while still open.
 */
@Component({
  selector: 'staff-complaint-detail',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  templateUrl: './complaint-detail.component.html',
  styleUrl: './complaint-detail.component.scss',
})
export class ComplaintDetailComponent implements OnInit {
  readonly categoryLabel = complaintCategoryLabel;
  readonly resolutionLabel = complaintResolutionLabel;
  readonly resolvedStatus = CinemaServiceAgent.ComplaintStatus.Resolved;

  complaint: CinemaServiceAgent.ComplaintDTO | null = null;
  loading = false;
  busy = false;

  private readonly _api = inject(CinemaServiceAgent.HttpService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _store = inject(Store);
  private readonly _dialog = inject(MatDialog);
  private readonly _dialogs = inject(DialogService);
  private readonly _sensitive = inject(SensitiveCallService);
  private readonly _overrides = inject(OverrideFlowService);
  private readonly _translate = inject(TranslateService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _cd = inject(ChangeDetectorRef);

  get actions(): ComplaintAction[] {
    return complaintActions(this.complaint?.status);
  }

  get closed(): boolean {
    return isComplaintClosed(this.complaint?.status);
  }

  ngOnInit(): void {
    this.loading = true;
    this._api.getComplaint(CinemaServiceAgent.GetComplaintRequest.fromJS({ complaintId: this._route.snapshot.paramMap.get('id') })).subscribe({
      next: complaint => {
        this.complaint = complaint;
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading = false;
      this._cd.markForCheck();
    });
  }

  back(): void {
    this._router.navigate(['/customer-service/complaints']);
  }

  edit(): void {
    const data: ComplaintDialogData = { complaint: this.complaint! };
    this._dialog.open<ComplaintDialogComponent, ComplaintDialogData, CinemaServiceAgent.ComplaintDTO>(
      ComplaintDialogComponent, { width: '520px', maxWidth: '95vw', data },
    ).afterClosed().subscribe(updated => {
      if (updated) {
        this._set(updated);
      }
    });
  }

  startReview(): void {
    this._run(this._api.startComplaintReview(CinemaServiceAgent.StartComplaintReviewRequest.fromJS({ complaintId: this.complaint!.id })), 'customerService.detail.reviewStarted');
  }

  reject(): void {
    this._dialogs.openReasonDialog({
      titleKey: 'customerService.detail.rejectTitle',
      hintKey: 'customerService.detail.rejectHint',
      confirmKey: 'customerService.detail.reject',
      confirmColor: 'warn',
      note: { labelKey: 'customerService.detail.rejectReason', required: true },
    }).afterClosed().subscribe(result => {
      if (!result?.note) {
        return;
      }
      this._run(this._api.rejectComplaint(CinemaServiceAgent.RejectComplaintRequest.fromJS({ complaintId: this.complaint!.id, reason: result.note })), 'customerService.detail.rejected');
    });
  }

  resolve(): void {
    const complaint = this.complaint!;
    const data: ResolveComplaintDialogData = { complaint, isApprover: this._overrides.isApprover() };
    this._dialog.open<ResolveComplaintDialogComponent, ResolveComplaintDialogData, ResolveComplaintChoice>(
      ResolveComplaintDialogComponent, { width: '520px', maxWidth: '95vw', data },
    ).afterClosed().subscribe(choice => {
      if (!choice) {
        return;
      }
      this.busy = true;
      this._store.dispatch(showLoading());
      this._sensitive.run(
        complaint.theaterId ?? this._theater.currentTheaterId() ?? undefined,
        resolutionNeedsPin(choice.resolution, this._overrides.isApprover()),
        override => this._api.resolveComplaint(CinemaServiceAgent.ResolveComplaintRequest.fromJS({
          complaintId: complaint.id,
          resolution: choice.resolution,
          amount: choice.amount,
          note: choice.note,
          refundTender: choice.refundTender,
          refundReference: choice.refundReference,
          override,
        })),
      ).subscribe({
        next: updated => {
          this._set(updated);
          this._store.dispatch(showSuccess({ message: this._translate.instant('customerService.detail.resolved', { resolution: this._translate.instant(complaintResolutionLabel(updated.resolution)) }) }));
        },
        error: error => this._store.dispatch(showException({ error })),
      }).add(() => {
        this.busy = false;
        this._store.dispatch(hideLoading());
        this._cd.markForCheck();
      });
    });
  }

  private _run(call: Observable<CinemaServiceAgent.ComplaintDTO>, successKey: string): void {
    this.busy = true;
    this._store.dispatch(showLoading());
    call.subscribe({
      next: updated => {
        this._set(updated);
        this._store.dispatch(showSuccess({ message: this._translate.instant(successKey) }));
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.busy = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  private _set(complaint: CinemaServiceAgent.ComplaintDTO): void {
    this.complaint = complaint;
    this._cd.markForCheck();
  }
}
