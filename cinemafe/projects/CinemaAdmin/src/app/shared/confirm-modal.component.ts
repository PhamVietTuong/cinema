import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { SharedModule } from 'CinemaLib';

/** Scrim + modal confirmation popup (same overlay pattern as the seating-chart popup). */
@Component({
  selector: 'app-confirm-modal',
  standalone: true,
  imports: [SharedModule],
  template: `
    <ng-container *ngIf="open">
      <div class="modal-scrim" (click)="closed.emit()"></div>
      <div class="modal-card modal-card--sm">
        <div class="modal-head">
          <h3 class="ad-card-title"><mat-icon class="is-danger">warning</mat-icon> {{ title || ('common.confirm' | translate) }}</h3>
          <button class="ad-icon-btn" type="button" (click)="closed.emit()" [title]="'common.close' | translate"><mat-icon>close</mat-icon></button>
        </div>
        <div class="modal-body">
          <p class="modal-msg">{{ message }}</p>
          <div class="form-actions">
            <button class="ad-btn ad-btn--ghost" type="button" (click)="closed.emit()">{{ cancelText || ('common.cancel' | translate) }}</button>
            <button class="ad-btn ad-btn--danger" type="button" (click)="confirmed.emit()">{{ confirmText || ('common.delete' | translate) }}</button>
          </div>
        </div>
      </div>
    </ng-container>
  `,
})
export class ConfirmModalComponent {
  @Input() open = false;
  /** Empty inputs fall back to translated defaults (common.confirm/delete/cancel) in the template. */
  @Input() title = '';
  @Input() message = '';
  @Input() confirmText = '';
  @Input() cancelText = '';
  @Output() confirmed = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open) { this.closed.emit(); }
  }
}
