import { NgModule } from '@angular/core';
import { SharedModule } from './shared.module';
import { ConfirmDialog } from './components/dialogs';

/**
 * Central hub NgModule for CinemaLib. Both CinemaAdmin and CinemaUser import
 * this once, in their root AppModule, instead of importing SharedModule and
 * individual library components separately — new shared, NgModule-usable
 * pieces get added to this module's imports/exports as they're built.
 */
@NgModule({
  imports: [SharedModule, ConfirmDialog],
  exports: [SharedModule, ConfirmDialog],
})
export class CinemaLibModule {}
