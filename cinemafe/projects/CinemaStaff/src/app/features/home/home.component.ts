import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { SharedModule, selectCurrentUser } from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';

/** Placeholder landing page: greets the user and shows which theater the staff tools are scoped to. */
@Component({
  selector: 'staff-home',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  readonly theater = inject(TheaterContextService);
  readonly user = inject(Store).selectSignal(selectCurrentUser);
}
