import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PosComponent } from './pos.component';

/** Counter point of sale for sellers. Mounted behind roleGuard(SELLER_ROLES) via a pass-through entry in app.routes.ts. */
const routes: Routes = [{ path: 'pos', component: PosComponent }];

@NgModule({
  imports: [PosComponent, RouterModule.forChild(routes)],
})
export class PosModule {}
