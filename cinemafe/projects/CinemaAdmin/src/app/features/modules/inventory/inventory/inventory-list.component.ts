import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** Placeholder — replaced by the real inventory page. */
@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [TranslatePipe],
  template: `<div class="ad-page"><h1 class="ad-h1">{{ 'warehouse.pageTitle.inventory' | translate }}</h1></div>`,
})
export class InventoryListComponent {}
