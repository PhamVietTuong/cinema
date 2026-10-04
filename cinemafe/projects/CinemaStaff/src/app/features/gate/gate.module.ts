import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from 'CinemaLib';

import { GateScanComponent } from './gate-scan.component';
import { GateLookupComponent } from './gate-lookup.component';

/** Gate page: scan tab (default) and lookup tab. */
@Component({
  selector: 'staff-gate-page',
  standalone: true,
  imports: [SharedModule, GateScanComponent, GateLookupComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'gate.title' | translate }}</h1>
      <p class="ad-sub">{{ 'gate.subtitle' | translate }}</p>
    </div>
  </div>
  <mat-tab-group animationDuration="0ms">
    <mat-tab [label]="'gate.tabScan' | translate"><div class="tab-body"><staff-gate-scan /></div></mat-tab>
    <mat-tab [label]="'gate.tabLookup' | translate"><div class="tab-body"><staff-gate-lookup /></div></mat-tab>
  </mat-tab-group>
</div>
`,
  styles: [`.tab-body { padding-top: 16px; }`],
})
export class GatePageComponent {}

/** Ticket gate for GATE_KEEPER_ROLES, mounted at /gate behind roleGuard in app.routes.ts. */
const routes: Routes = [
  { path: '', component: GatePageComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
})
export class GateModule {}
