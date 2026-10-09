import { Pipe, PipeTransform } from '@angular/core';
import { fromServerUtc } from '../utils/server-time';

/** Re-reads a server UTC timestamp so the following `date` pipe shows local time: `{{ x.openedAt | serverUtc | date:'HH:mm' }}`. */
@Pipe({ name: 'serverUtc', standalone: true })
export class ServerUtcPipe implements PipeTransform {
  transform(value: Date | string | null | undefined): Date | null {
    return fromServerUtc(value) ?? null;
  }
}
