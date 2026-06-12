import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';

type Dto = CinemaServiceAgent.TicketTypeDTO;

@Component({
  selector: 'app-ticket-types',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './ticket-types.component.html',
})
export class TicketTypesManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({
      name: ['', Validators.required],
      basePrice: [0, [Validators.required, Validators.min(0)]],
      description: [''],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) { return this._svc.getTicketTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })); }
  create(v: any) { return this._svc.createTicketType(CinemaServiceAgent.CreateTicketTypeRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateTicketType(CinemaServiceAgent.UpdateTicketTypeRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteTicketType(id); }
}
