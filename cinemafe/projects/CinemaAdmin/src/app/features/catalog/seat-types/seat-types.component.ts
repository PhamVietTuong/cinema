import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.SeatTypeDTO;

@Component({
  selector: 'app-seat-types',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './seat-types.component.html',
})
export class SeatTypesManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({
      name: ['', Validators.required],
      color: ['#808080', Validators.required],
      // Seat price = showtime base price × this multiplier (1 = standard, 1.5 = VIP, 2 = double…).
      priceMultiplier: [1, [Validators.required, Validators.min(0.1)]],
      description: [''],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) { return this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })); }
  create(v: any) { return this._svc.createSeatType(CinemaServiceAgent.CreateSeatTypeRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateSeatType(CinemaServiceAgent.UpdateSeatTypeRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteSeatType(id); }
}
