import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** Placeholder — replaced by the real storage plan list page. */
@Component({
  selector: 'app-storage-plan-list',
  standalone: true,
  imports: [TranslatePipe],
  template: `<div class="ad-page"><h1 class="ad-h1">{{ 'warehouse.pageTitle.storagePlans' | translate }}</h1></div>`,
})
export class StoragePlanListComponent {}
