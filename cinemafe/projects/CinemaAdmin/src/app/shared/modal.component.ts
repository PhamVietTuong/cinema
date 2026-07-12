import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { SharedModule } from 'CinemaLib';

/**
 * Scrim + centered modal shell (same overlay pattern as the seating-chart popup).
 * Renders projected content when `open` is true; emits `closed` on scrim click,
 * the X button, or Escape.
 */
@Component({
  selector: 'app-modal',
  standalone: true,
  imports: [SharedModule],
  template: `
    <ng-container *ngIf="open">
      <div class="modal-scrim" (click)="closed.emit()"></div>
      <div class="modal-card" [class.modal-card--wide]="wide">
        <div class="modal-head">
          <h3 class="ad-card-title" *ngIf="title">{{ title }}</h3>
          <button class="ad-icon-btn" type="button" (click)="closed.emit()" [title]="'common.close' | translate"><mat-icon>close</mat-icon></button>
        </div>
        <div class="modal-body"><ng-content></ng-content></div>
      </div>
    </ng-container>
  `,
})
export class ModalComponent {
  @Input() open = false;
  @Input() title = '';
  @Input() wide = false;
  @Output() closed = new EventEmitter<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open) { this.closed.emit(); }
  }
}
