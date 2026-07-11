import { Component, Input, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog/catalog-crud.base';
import { ModalComponent } from '../../shared/modal.component';
import { ConfirmModalComponent } from '../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.RoomTypeDTO;

/** Screening-room-type management scoped to a single theater (2D/3D/IMAX/4DX…). */
@Component({
  selector: 'app-theater-room-types',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './theater-room-types.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterRoomTypesComponent extends CatalogCrudBase<Dto> {
  @Input({ required: true }) theaterId!: string;
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({
      name: ['', Validators.required],
      description: [''],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getRoomTypes(CinemaServiceAgent.PagingSearchDTO.fromJS(
      { pageIndex, pageSize, filters: { ...filters, theaterId: this.theaterId } }));
  }
  create(v: any) { return this._svc.createRoomType(CinemaServiceAgent.CreateRoomTypeRequest.fromJS({ ...v, theaterId: this.theaterId })); }
  update(v: any, id: string) { return this._svc.updateRoomType(CinemaServiceAgent.UpdateRoomTypeRequest.fromJS({ ...v, id, theaterId: this.theaterId })); }
  remove(id: string) { return this._svc.deleteRoomType(id); }
}
