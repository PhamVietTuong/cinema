import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';

type Dto = CinemaServiceAgent.MovieTypeDTO;

@Component({
  selector: 'app-movie-types',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './movie-types.component.html',
})
export class MovieTypesManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({ name: ['', Validators.required] });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) { return this._svc.getMovieTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })); }
  create(v: any) { return this._svc.createMovieType(CinemaServiceAgent.CreateMovieTypeRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateMovieType(CinemaServiceAgent.UpdateMovieTypeRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteMovieType(id); }
}
