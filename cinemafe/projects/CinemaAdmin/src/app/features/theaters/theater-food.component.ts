import { Component, Input, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog/catalog-crud.base';
import { ModalComponent } from '../../shared/modal.component';
import { ConfirmModalComponent } from '../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.FoodAndDrinkDTO;

/** Food & drink management scoped to a single theater. */
@Component({
  selector: 'app-theater-food',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './theater-food.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterFoodComponent extends CatalogCrudBase<Dto> {
  @Input({ required: true }) theaterId!: string;
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({
      name: ['', Validators.required],
      price: [0, [Validators.required, Validators.min(0)]],
      imageUrl: [''],
      description: [''],
      isAvailable: [true],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getFoodAndDrinks(CinemaServiceAgent.PagingSearchDTO.fromJS(
      { pageIndex, pageSize, filters: { ...filters, theaterId: this.theaterId } }));
  }
  create(v: any) { return this._svc.createFoodAndDrink(CinemaServiceAgent.CreateFoodAndDrinkRequest.fromJS({ ...v, theaterId: this.theaterId })); }
  update(v: any, id: string) { return this._svc.updateFoodAndDrink(CinemaServiceAgent.UpdateFoodAndDrinkRequest.fromJS({ ...v, id, theaterId: this.theaterId })); }
  remove(id: string) { return this._svc.deleteFoodAndDrink(id); }
}
