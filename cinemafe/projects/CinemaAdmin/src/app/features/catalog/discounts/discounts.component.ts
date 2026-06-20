import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.DiscountDTO;

@Component({
  selector: 'app-discounts',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './discounts.component.html',
})
export class DiscountsManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  discountTypes: CinemaServiceAgent.DiscountTypeDTO[] = [];

  override ngOnInit(): void {
    super.ngOnInit();
    this._svc.getDiscountTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .subscribe(r => { this.discountTypes = r.results ?? []; this._cdr.markForCheck(); });
  }

  buildForm() {
    return this._fb.group({
      code: ['', Validators.required],
      description: [''],
      percent: [0, [Validators.required, Validators.min(0)]],
      maxDiscountAmount: [null],
      discountTypeId: ['', Validators.required],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      maxUsage: [null],
      isActive: [true],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getDiscounts(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters }));
  }
  create(v: any) { return this._svc.createDiscount(CinemaServiceAgent.CreateDiscountRequest.fromJS(v)); }
  update(v: any, id: string) { return this._svc.updateDiscount(CinemaServiceAgent.UpdateDiscountRequest.fromJS({ ...v, id })); }
  remove(id: string) { return this._svc.deleteDiscount(id); }

  protected override toFormValue(i: Dto) {
    return {
      ...i,
      startDate: i.startDate ? new Date(i.startDate).toISOString().split('T')[0] : '',
      endDate: i.endDate ? new Date(i.endDate).toISOString().split('T')[0] : '',
    };
  }

  typeName(id?: string): string {
    return this.discountTypes.find(t => t.id === id)?.name ?? '—';
  }
}
