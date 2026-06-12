import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';

type Dto = CinemaServiceAgent.AgeRestrictionDTO;

@Component({
  selector: 'app-age-restrictions',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './age-restrictions.component.html',
})
export class AgeRestrictionsManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({
      code: ['', Validators.required],
      description: ['', Validators.required],
      minAge: [0, [Validators.required, Validators.min(0)]],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) { return this._svc.getAgeRestrictions(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })); }
  create(v: any) { return this._svc.createAgeRestriction(CinemaServiceAgent.CreateAgeRestrictionRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateAgeRestriction(CinemaServiceAgent.UpdateAgeRestrictionRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteAgeRestriction(id); }
}
