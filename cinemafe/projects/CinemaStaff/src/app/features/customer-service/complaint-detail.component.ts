import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { Observable } from 'rxjs';
import {
  DialogService, EmptyStateComponent, SharedModule, StaffServiceAgent, StatusPillComponent, complaintCategoryLabel, complaintResolutionLabel,
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
  template: `
<div class="ad-page">
  @if (complaint; as c) {
    <div class="ad-page-header">
      <div>
        <button class="ad-btn ad-btn--ghost" type="button" (click)="back()"><mat-icon>arrow_back</mat-icon> {{ 'opsCommon.back' | translate }}</button>
        <h1 class="ad-h1">{{ categoryLabel(c.category) | translate }}
          <cl-status-pill kind="complaint" [value]="c.status" />
        </h1>
        <p class="ad-sub">{{ c.createdByName }} · {{ c.creationTime | date: 'dd/MM/yyyy HH:mm' }}</p>
      </div>
    </div>

    <mat-card class="card">
      <dl class="facts">
        <dt>{{ 'customerService.complaint.description' | translate }}</dt>
        <dd class="text">{{ c.description }}</dd>
        @if (c.customerName) {
          <dt>{{ 'customerService.complaint.customer' | translate }}</dt>
          <dd>{{ c.customerName }}</dd>
        }
        @if (c.invoiceCode) {
          <dt>{{ 'customerService.complaint.invoice' | translate }}</dt>
          <dd>{{ c.invoiceCode }}</dd>
        }
        @if (c.assignedToName) {
          <dt>{{ 'customerService.complaint.assignedTo' | translate }}</dt>
          <dd>{{ c.assignedToName }}</dd>
        }
        @if (closed) {
          <dt>{{ 'customerService.detail.outcome' | translate }}</dt>
          <dd>
            @if (c.status === resolvedStatus) {
              <strong>{{ resolutionLabel(c.resolution) | translate }}</strong>
              @if (c.compensationAmount) { · {{ c.compensationAmount | number:'1.0-0' }} }
              @if (c.compensationRef) { · {{ c.compensationRef }} }
            }
            {{ c.resolvedByName }} · {{ c.resolvedAt | date: 'dd/MM/yyyy HH:mm' }}
            @if (c.resolutionNote) { <br>{{ c.resolutionNote }} }
          </dd>
        }
      </dl>
    </mat-card>

    @if (actions.length) {
      <mat-card class="card">
        <h3 class="ad-card-title">{{ 'customerService.detail.workflow' | translate }}</h3>
        <div class="actions">
          @if (actions.includes('edit')) {
            <button mat-stroked-button type="button" [disabled]="busy" (click)="edit()"><mat-icon>edit</mat-icon> {{ 'customerService.detail.edit' | translate }}</button>
          }
          @if (actions.includes('startReview')) {
            <button mat-stroked-button type="button" [disabled]="busy" (click)="startReview()"><mat-icon>play_arrow</mat-icon> {{ 'customerService.detail.startReview' | translate }}</button>
          }
          @if (actions.includes('reject')) {
            <button mat-stroked-button color="warn" type="button" [disabled]="busy" (click)="reject()"><mat-icon>block</mat-icon> {{ 'customerService.detail.reject' | translate }}</button>
          }
          @if (actions.includes('resolve')) {
            <button mat-raised-button color="primary" type="button" [disabled]="busy" (click)="resolve()"><mat-icon>task_alt</mat-icon> {{ 'customerService.detail.resolve' | translate }}</button>
          }
        </div>
      </mat-card>
    }
  } @else if (!loading) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="report_problem" messageKey="customerService.detail.notFound" />
    </mat-card>
  }
</div>
`,
  styles: [`
    .card { padding: 16px 24px; margin-bottom: 16px; }
    .facts { display: grid; grid-template-columns: max-content 1fr; gap: 8px 24px; margin: 0; }
    .facts dt { color: var(--ml-muted); }
    .facts dd { margin: 0; }
    .text { white-space: pre-wrap; }
    .actions { display: flex; gap: 12px; flex-wrap: wrap; margin-top: 8px; }
  `],
})
export class ComplaintDetailComponent implements OnInit {
  readonly categoryLabel = complaintCategoryLabel;
  readonly resolutionLabel = complaintResolutionLabel;
  readonly resolvedStatus = StaffServiceAgent.ComplaintStatus.Resolved;

  complaint: StaffServiceAgent.ComplaintDTO | null = null;
  loading = false;
  busy = false;

  private readonly _api = inject(StaffServiceAgent.CustomerServiceHttpService);
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
    this._api.getComplaint(StaffServiceAgent.GetComplaintRequest.fromJS({ complaintId: this._route.snapshot.paramMap.get('id') })).subscribe({
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
    this._dialog.open<ComplaintDialogComponent, ComplaintDialogData, StaffServiceAgent.ComplaintDTO>(
      ComplaintDialogComponent, { width: '520px', maxWidth: '95vw', data },
    ).afterClosed().subscribe(updated => {
      if (updated) {
        this._set(updated);
      }
    });
  }

  startReview(): void {
    this._run(this._api.startComplaintReview(StaffServiceAgent.StartComplaintReviewRequest.fromJS({ complaintId: this.complaint!.id })), 'customerService.detail.reviewStarted');
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
      this._run(this._api.rejectComplaint(StaffServiceAgent.RejectComplaintRequest.fromJS({ complaintId: this.complaint!.id, reason: result.note })), 'customerService.detail.rejected');
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
        override => this._api.resolveComplaint(StaffServiceAgent.ResolveComplaintRequest.fromJS({
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

  private _run(call: Observable<StaffServiceAgent.ComplaintDTO>, successKey: string): void {
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

  private _set(complaint: StaffServiceAgent.ComplaintDTO): void {
    this.complaint = complaint;
    this._cd.markForCheck();
  }
}
