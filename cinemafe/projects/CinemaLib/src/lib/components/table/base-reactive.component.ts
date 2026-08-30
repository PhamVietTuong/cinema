import { Directive, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';

@Directive()
export abstract class BaseReactiveComponent implements OnDestroy {

  protected readonly ngDestroyed$: Subject<void> = new Subject<void>();

  public ngOnDestroy(): void {
    this.ngDestroyed$.next();
    this.ngDestroyed$.complete();
  }
}
