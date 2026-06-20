import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.UserTypeDTO;

@Component({
  selector: 'app-user-types',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './user-types.component.html',
})
export class UserTypesManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({ name: ['', Validators.required] });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) { return this._svc.getUserTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })); }
  create(v: any) { return this._svc.createUserType(CinemaServiceAgent.CreateUserTypeRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateUserType(CinemaServiceAgent.UpdateUserTypeRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteUserType(id); }
}
