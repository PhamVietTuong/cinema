import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { Store } from '@ngrx/store';
import { APPROVER_ROLES, SharedModule, selectCurrentUser } from 'CinemaLib';
import { DailyCloseComponent } from './daily-close.component';
import { MyDrawerComponent } from './my-drawer.component';

/** Cash close page: my drawer tab for every seller, daily close tab for approvers only. */
@Component({
  selector: 'staff-cash-close-page',
  standalone: true,
  imports: [SharedModule, MyDrawerComponent, DailyCloseComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'cashClose.title' | translate }}</h1>
      <p class="ad-sub">{{ 'cashClose.subtitle' | translate }}</p>
    </div>
  </div>
  <mat-tab-group animationDuration="0ms">
    <mat-tab [label]="'cashClose.tabDrawer' | translate"><div class="tab-body"><staff-my-drawer /></div></mat-tab>
    @if (isApprover()) {
      <mat-tab [label]="'cashClose.tabDaily' | translate"><div class="tab-body"><staff-daily-close /></div></mat-tab>
    }
  </mat-tab-group>
</div>
`,
  styles: [`.tab-body { padding-top: 16px; }`],
})
export class CashClosePageComponent {
  private readonly _user = inject(Store).selectSignal(selectCurrentUser);

  isApprover(): boolean {
    return APPROVER_ROLES.includes(this._user()?.userTypeName ?? '');
  }
}

/** Drawer close and daily close for SELLER_ROLES, mounted at /cash-close behind roleGuard in app.routes.ts. */
const routes: Routes = [
  { path: '', component: CashClosePageComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
})
export class CashCloseModule {}
