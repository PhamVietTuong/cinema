import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import {
  SharedModule, CinemaServiceAgent,
  PromotionMechanicValues, PromotionEligibilityValues, daysMaskToList, channelsMaskToList,
} from 'CinemaLib';

type ProgramDTO = CinemaServiceAgent.PromotionProgramDTO;

@Component({
  selector: 'app-promotions',
  standalone: true,
  imports: [SharedModule, RouterLink],
  templateUrl: './promotions.component.html',
  styleUrl: './promotions.component.scss',
})
export class PromotionsComponent implements OnInit {
  private _cinema = inject(CinemaServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);
  private _translate = inject(TranslateService);

  programs: ProgramDTO[] = [];
  loading = true;
  private _expanded = new Set<string>();
  private _copiedId: string | null = null;

  ngOnInit(): void {
    this._cinema.getPublishedPromotionPrograms(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 50 }))
      .subscribe({
        next: r => { this.programs = r.results ?? []; this.loading = false; this._cdr.markForCheck(); },
        error: () => { this.loading = false; this._cdr.markForCheck(); },
      });
  }

  // ── Display helpers ──────────────────────────────────────────────────────────
  mechanicLabel(p: ProgramDTO): string | null {
    if (p.mechanic === CinemaServiceAgent.PromotionMechanic.Info) {
      return null;
    }
    const key = PromotionMechanicValues.find(x => x.value === p.mechanic)?.name ?? '';
    return key;
  }

  eligibilityLabel(p: ProgramDTO): string {
    return PromotionEligibilityValues.find(x => x.value === p.eligibility)?.name ?? '';
  }

  hasStarted(p: ProgramDTO): boolean {
    return !p.startDate || new Date(p.startDate as any).getTime() <= Date.now();
  }

  theaterCountLabel(p: ProgramDTO): string {
    const count = new Set((p.schedules ?? []).map(s => s.theaterId).filter(id => !!id)).size;
    return count === 0 ? 'promotions.allTheaters' : 'promotions.theatersCount';
  }

  theaterCount(p: ProgramDTO): number {
    return new Set((p.schedules ?? []).map(s => s.theaterId).filter(id => !!id)).size;
  }

  /** "Every day" when unset/all 7 days, else a joined, translated list of abbreviated day names. */
  dayLabelsText(mask?: number | null): string {
    const days = daysMaskToList(mask);
    if (days.length === 0 || days.length === 7) {
      return this._translate.instant('promotions.everyDay');
    }
    const keys = days.map(d => `common.days.${['sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat'][d]}Short`);
    return keys.map(k => this._translate.instant(k)).join(', ');
  }

  channelLabels(p: ProgramDTO): string[] {
    const channels = channelsMaskToList(p.allowedChannelsMask);
    if (channels.length === 0) {
      return [];
    }
    return channels.map(c => c === CinemaServiceAgent.SalesChannel.Online ? 'promoProgram.channel.online' : 'promoProgram.channel.counter');
  }

  termsLines(p: ProgramDTO): string[] {
    return (p.termsText ?? '').split('\n').map(l => l.trim()).filter(l => l.length > 0);
  }

  // ── Expand/collapse ──────────────────────────────────────────────────────────
  isExpanded(id?: string): boolean {
    return !!id && this._expanded.has(id);
  }

  toggle(id?: string): void {
    if (!id) {
      return;
    }
    if (this._expanded.has(id)) { this._expanded.delete(id); } else { this._expanded.add(id); }
    this._cdr.markForCheck();
  }

  isCopied(id?: string): boolean {
    return !!id && this._copiedId === id;
  }

  copyCode(id: string | undefined, code: string): void {
    if (!id) {
      return;
    }
    navigator.clipboard?.writeText(code).then(() => {
      this._copiedId = id;
      this._cdr.markForCheck();
      setTimeout(() => {
        if (this._copiedId === id) {
          this._copiedId = null;
          this._cdr.markForCheck();
        }
      }, 2000);
    });
  }
}
