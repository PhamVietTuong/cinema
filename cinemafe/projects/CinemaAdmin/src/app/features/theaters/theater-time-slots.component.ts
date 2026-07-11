import { Component, Input, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog/catalog-crud.base';
import { ModalComponent } from '../../shared/modal.component';
import { ConfirmModalComponent } from '../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.TimeSlotDTO;

/** Time-slot management scoped to a single theater (named hour ranges for pricing). */
@Component({
  selector: 'app-theater-time-slots',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './theater-time-slots.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterTimeSlotsComponent extends CatalogCrudBase<Dto> {
  @Input({ required: true }) theaterId!: string;
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({
      name: ['', Validators.required],
      startTime: ['08:00', Validators.required],
      endTime: ['12:00', Validators.required],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getTimeSlots(CinemaServiceAgent.PagingSearchDTO.fromJS(
      { pageIndex, pageSize, filters: { ...filters, theaterId: this.theaterId } }));
  }
  create(v: any) { return this._svc.createTimeSlot(CinemaServiceAgent.CreateTimeSlotRequest.fromJS({ ...v, theaterId: this.theaterId })); }
  update(v: any, id: string) { return this._svc.updateTimeSlot(CinemaServiceAgent.UpdateTimeSlotRequest.fromJS({ ...v, id, theaterId: this.theaterId })); }
  remove(id: string) { return this._svc.deleteTimeSlot(id); }
}
