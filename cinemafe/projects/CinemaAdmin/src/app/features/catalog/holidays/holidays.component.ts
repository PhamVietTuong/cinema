import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.HolidayDTO;

@Component({
  selector: 'app-holidays',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './holidays.component.html',
})
export class HolidaysManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({
      name: ['', Validators.required],
      date: ['', Validators.required],
      priceMultiplier: [1.5, [Validators.required, Validators.min(0)]],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) { return this._svc.getHolidays(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })); }
  create(v: any) { return this._svc.createHoliday(CinemaServiceAgent.CreateHolidayRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateHoliday(CinemaServiceAgent.UpdateHolidayRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteHoliday(id); }

  protected override toFormValue(i: Dto) {
    return { ...i, date: i.date ? new Date(i.date).toISOString().split('T')[0] : '' };
  }
}
