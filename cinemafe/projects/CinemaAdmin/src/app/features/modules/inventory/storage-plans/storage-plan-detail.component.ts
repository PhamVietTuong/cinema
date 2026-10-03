import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** Placeholder — replaced by the real storage plan detail page. */
@Component({
  selector: 'app-storage-plan-detail',
  standalone: true,
  imports: [TranslatePipe],
  template: `<div class="ad-page"><h1 class="ad-h1">{{ 'warehouse.pageTitle.storagePlans' | translate }}</h1></div>`,
})
export class StoragePlanDetailComponent {}
