import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';

type Dto = CinemaServiceAgent.SeatTypeDTO;

@Component({
  selector: 'app-seat-types',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './seat-types.component.html',
})
export class SeatTypesManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({
      name: ['', Validators.required],
      color: ['#808080', Validators.required],
      description: [''],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) { return this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })); }
  create(v: any) { return this._svc.createSeatType(CinemaServiceAgent.CreateSeatTypeRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateSeatType(CinemaServiceAgent.UpdateSeatTypeRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteSeatType(id); }
}
