import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.FoodAndDrinkDTO;

@Component({
  selector: 'app-food-and-drinks',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './food-and-drinks.component.html',
})
export class FoodAndDrinksManagementComponent extends CatalogCrudBase<Dto> {
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
    return this._svc.getFoodAndDrinks(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters }));
  }
  create(v: any) { return this._svc.createFoodAndDrink(CinemaServiceAgent.CreateFoodAndDrinkRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateFoodAndDrink(CinemaServiceAgent.UpdateFoodAndDrinkRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteFoodAndDrink(id); }
}
