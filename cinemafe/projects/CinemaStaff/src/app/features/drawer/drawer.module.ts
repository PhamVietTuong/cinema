import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DrawerComponent } from './drawer.component';

/** Cash drawer page for sellers. Mounted behind roleGuard(SELLER_ROLES) via a pass-through entry in app.routes.ts. */
const routes: Routes = [{ path: 'drawer', component: DrawerComponent }];

@NgModule({
  imports: [DrawerComponent, RouterModule.forChild(routes)],
})
export class DrawerModule {}
