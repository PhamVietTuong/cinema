import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent } from 'CinemaLib';
import { renderTemplatePreview } from './template-preview';

export interface TemplatePreviewDialogData {
  template: CinemaServiceAgent.TemplateDTO;
}

/** Read-only preview of an email template rendered with sample booking data. */
@Component({
  selector: 'app-template-preview-dialog',
  standalone: false,
  template: `
    <div mat-dialog-title class="dialog-title">{{ 'templates.preview' | translate }}: {{ name }}</div>
    <mat-dialog-content>
      <!-- Sandboxed with no permissions: the body is admin-authored HTML. -->
      <iframe *ngIf="previewHtml" sandbox="" [srcdoc]="previewHtml" title="preview"
        style="width: 100%; height: 640px; border: 1px solid #e0d6c6; border-radius: 8px; background: #fff;"></iframe>
    </mat-dialog-content>
    <div mat-dialog-actions class="dialog-actions">
      <button mat-raised-button type="button" (click)="close()">{{ 'common.close' | translate }}</button>
    </div>
    <button mat-icon-button type="button" class="dialog-close-btn" (click)="close()">
      <mat-icon>close</mat-icon>
    </button>
  `,
})
export class TemplatePreviewDialog implements OnInit {
  readonly name: string;
  previewHtml: SafeHtml | null = null;

  constructor(
    private _dialogRef: MatDialogRef<TemplatePreviewDialog>,
    private _sanitizer: DomSanitizer,
    private _cd: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) private _data: TemplatePreviewDialogData,
  ) {
    this.name = _data.template.name ?? '';
  }

  async ngOnInit(): Promise<void> {
    const html = await renderTemplatePreview(this._data.template.body ?? '', true);
    this.previewHtml = this._sanitizer.bypassSecurityTrustHtml(html);
    this._cd.markForCheck();
  }

  close(): void {
    this._dialogRef.close();
  }
}
