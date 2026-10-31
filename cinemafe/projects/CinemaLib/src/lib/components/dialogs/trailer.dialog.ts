import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';
import { toYouTubeEmbedUrl } from '../../utils/video-embed';

export interface TrailerDialogData {
  title: string;
  trailerUrl: string;
}

/**
 * Plays a movie's trailer in a modal, opened via DialogService.openTrailerDialog(). Only the parsed
 * YouTube embed URL is ever sanitized/bound to the iframe `src` — the raw `trailerUrl` is only ever
 * used as a plain `href` for the "open externally" fallback, never bypassed.
 */
@Component({
  selector: 'cl-trailer-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule, TranslatePipe],
  templateUrl: './trailer.dialog.html',
  styleUrl: './trailer.dialog.scss',
})
export class TrailerDialogComponent {
  readonly embedUrl: SafeResourceUrl | null;

  constructor(
    private _dialogRef: MatDialogRef<TrailerDialogComponent, void>,
    @Inject(MAT_DIALOG_DATA) public data: TrailerDialogData,
    sanitizer: DomSanitizer,
  ) {
    const resolved = toYouTubeEmbedUrl(data.trailerUrl);
    this.embedUrl = resolved ? sanitizer.bypassSecurityTrustResourceUrl(resolved) : null;
  }

  close(): void {
    this._dialogRef.close();
  }
}
