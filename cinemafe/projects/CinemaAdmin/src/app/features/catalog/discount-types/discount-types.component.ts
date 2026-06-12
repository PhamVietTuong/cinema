import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';

type Dto = CinemaServiceAgent.DiscountTypeDTO;

@Component({
  selector: 'app-discount-types',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './discount-types.component.html',
})
export class DiscountTypesManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({ name: ['', Validators.required] });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) { return this._svc.getDiscountTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })); }
  create(v: any) { return this._svc.createDiscountType(CinemaServiceAgent.CreateDiscountTypeRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateDiscountType(CinemaServiceAgent.UpdateDiscountTypeRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteDiscountType(id); }
}
