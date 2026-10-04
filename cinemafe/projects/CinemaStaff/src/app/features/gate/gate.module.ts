import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { EmptyStateComponent, SharedModule } from 'CinemaLib';

import { TheaterContextService } from '../../core/theater-context.service';

import { GateScanComponent } from './gate-scan.component';
import { GateLookupComponent } from './gate-lookup.component';

/** Gate page: the scan card and the lookup card side by side (stacked on narrow screens). */
@Component({
  selector: 'staff-gate-page',
  standalone: true,
  imports: [SharedModule, EmptyStateComponent, GateScanComponent, GateLookupComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'gate.title' | translate }}</h1>
      <p class="ad-sub">{{ 'gate.subtitle' | translate }}</p>
    </div>
  </div>
  @if (!theaterId()) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="theaters" messageKey="gate.pickTheater" hintKey="gate.pickTheaterHint" />
    </mat-card>
  } @else {
    <div class="gate-grid">
      <staff-gate-scan class="ad-card" />
      <staff-gate-lookup class="ad-card" />
    </div>
  }
</div>
`,
  styles: [`
    .gate-grid { display: grid; grid-template-columns: 1.2fr 1fr; gap: 18px; align-items: start; }
    .gate-grid > * { display: block; min-width: 0; padding: 20px; }
    @media (max-width: 900px) { .gate-grid { grid-template-columns: 1fr; } }
  `],
})
export class GatePageComponent {
  readonly theaterId = inject(TheaterContextService).currentTheaterId;
}

/** Ticket gate for GATE_KEEPER_ROLES, mounted at /gate behind roleGuard in app.routes.ts. */
const routes: Routes = [
  { path: '', component: GatePageComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
})
export class GateModule {}
