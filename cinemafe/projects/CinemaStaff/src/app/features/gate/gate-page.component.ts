import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
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
  templateUrl: './gate-page.component.html',
  styleUrl: './gate-page.component.scss',
})
export class GatePageComponent {
  readonly theaterId = inject(TheaterContextService).currentTheaterId;
}
