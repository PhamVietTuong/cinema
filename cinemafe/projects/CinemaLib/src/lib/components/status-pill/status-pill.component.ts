import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { StatusPillKind, statusPillSpec } from '../../interfaces/cinema.model';

/**
 * Coloured status pill. The label key and colour for each kind/value live in `interfaces/cinema.model.ts`
 * (`statusPillSpec`), so pages never carry their own status maps.
 *
 * Usage: `<cl-status-pill kind="storagePlan" [value]="plan.status" />`
 */
@Component({
  selector: 'cl-status-pill',
  standalone: true,
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './status-pill.component.html',
})
export class StatusPillComponent {
  readonly kind = input.required<StatusPillKind>();
  readonly value = input<unknown>();

  protected readonly spec = computed(() => statusPillSpec(this.kind(), this.value()));
}
