import { Component, Inject } from '@angular/core';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';
import { CinemaServiceAgent } from 'CinemaLib';

/** The CinemaUser popup shown once per browser session for the active SplashBanner. Clicking the
 * image opens LinkUrl (if set) in a new tab and closes the dialog; the X just closes it. */
@Component({
  selector: 'app-splash-banner-dialog',
  standalone: true,
  imports: [MatDialogModule, MatIconModule, TranslatePipe],
  templateUrl: './splash-banner.dialog.html',
  styleUrl: './splash-banner.dialog.scss',
})
export class SplashBannerDialogComponent {
  constructor(
    private _dialogRef: MatDialogRef<SplashBannerDialogComponent, void>,
    @Inject(MAT_DIALOG_DATA) public data: CinemaServiceAgent.SplashBannerDTO,
  ) {
  }

  onImageClick(): void {
    if (this.data.linkUrl) {
      window.open(this.data.linkUrl, '_blank', 'noopener');
      this.close();
    }
  }

  close(): void {
    this._dialogRef.close();
  }
}
