import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.MemberShipDTO;

@Component({
  selector: 'app-memberships',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './memberships.component.html',
})
export class MembershipsManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({
      name: ['', Validators.required],
      minPoints: [0, [Validators.required, Validators.min(0)]],
      maxPoints: [0, [Validators.required, Validators.min(0)]],
      discountPercent: [0, [Validators.required, Validators.min(0)]],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) { return this._svc.getMemberShips(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })); }
  create(v: any) { return this._svc.createMemberShip(CinemaServiceAgent.CreateMemberShipRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateMemberShip(CinemaServiceAgent.UpdateMemberShipRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteMemberShip(id); }
}
