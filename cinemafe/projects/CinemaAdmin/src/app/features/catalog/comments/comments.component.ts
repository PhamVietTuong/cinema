import { Component, inject } from '@angular/core';
import { EMPTY, Observable } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, CinemaServiceAgent, ToastService } from 'CinemaLib';
import { FormGroup } from '@angular/forms';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.CommentModerationDTO;

@Component({
  selector: 'app-comments',
  standalone: true,
  imports: [SharedModule, ConfirmModalComponent],
  templateUrl: './comments.component.html',
})
export class CommentsModerationComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);
  private _toast = inject(ToastService);
  private _translate = inject(TranslateService);

  // No create/edit form on this screen — moderation happens through inline actions.
  buildForm(): FormGroup {
    return this._fb.group({});
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getCommentsForModeration(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters }));
  }
  create(): Observable<unknown> { return EMPTY; }
  update(): Observable<unknown> { return EMPTY; }
  remove(id: string) { return this._svc.deleteComment(CinemaServiceAgent.DeleteCommentRequest.fromJS({ commentId: id })); }

  /** Approve (show) or hide a comment, then reload the current page. */
  moderate(id?: string, approved?: boolean): void {
    if (!id) { return; }
    this._svc.moderateComment(CinemaServiceAgent.ModerateCommentRequest.fromJS({ commentId: id, approved }))
      .subscribe({
        next: () => {
          this._toast.success(this._translate.instant(approved ? 'comments.approveSuccess' : 'comments.hideSuccess'));
          this.load();
        },
        error: e => {
          this._toast.error(this._err(e, this._translate.instant('comments.moderateFailed')));
        },
      });
  }

  private _err(e: any, fallback: string): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.error || x?.message || fallback);
  }
}
