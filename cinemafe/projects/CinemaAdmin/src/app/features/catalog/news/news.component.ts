import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';

type Dto = CinemaServiceAgent.NewsDTO;

@Component({
  selector: 'app-news',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './news.component.html',
})
export class NewsManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  buildForm() {
    return this._fb.group({
      title: ['', Validators.required],
      content: ['', Validators.required],
      author: [''],
      thumbnailUrl: [''],
      isPublished: [false],
      publishedAt: [''],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) { return this._svc.getNewsList(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })); }
  create(v: any) { return this._svc.createNews(CinemaServiceAgent.CreateNewsRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateNews(CinemaServiceAgent.UpdateNewsRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteNews(id); }

  protected override toFormValue(i: Dto) {
    return { ...i, publishedAt: i.publishedAt ? new Date(i.publishedAt).toISOString().split('T')[0] : '' };
  }
}
