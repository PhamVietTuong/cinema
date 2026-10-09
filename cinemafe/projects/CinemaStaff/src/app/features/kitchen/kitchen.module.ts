import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { KitchenComponent } from './kitchen.component';

/** Food pickup queue for CONCESSION_ROLES, mounted at /kitchen behind roleGuard in app.routes.ts. */
const routes: Routes = [
  { path: '', component: KitchenComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
})
export class KitchenModule {}
