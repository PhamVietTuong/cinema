import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import {
  SharedModule, CinemaServiceAgent,
  PromotionMechanicValues, PromotionEligibilityValues, daysMaskToList, channelsMaskToList,
} from 'CinemaLib';

type ProgramDTO = CinemaServiceAgent.PromotionProgramDTO;
type PromoTab = 'all' | 'auto' | 'code' | 'members' | 'students';

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
  visible: ProgramDTO[] = [];
  loading = true;
  private _expanded = new Set<string>();
  private _copiedId: string | null = null;

  readonly tabs: { key: PromoTab; label: string }[] = [
    { key: 'all', label: 'promotions.tabs.all' },
    { key: 'auto', label: 'promotions.tabs.auto' },
    { key: 'code', label: 'promotions.tabs.code' },
    { key: 'members', label: 'promotions.tabs.members' },
    { key: 'students', label: 'promotions.tabs.students' },
  ];
  activeTab: PromoTab = 'all';

  ngOnInit(): void {
    this._cinema.getPublishedPromotionPrograms(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 50 }))
      .subscribe({
        next: r => { this.programs = r.results ?? []; this.applyFilter(); this.loading = false; this._cdr.markForCheck(); },
        error: () => { this.loading = false; this._cdr.markForCheck(); },
      });
  }

  // ── Tabs ─────────────────────────────────────────────────────────────────────
  setTab(key: PromoTab): void {
    this.activeTab = key;
    this.applyFilter();
    this._cdr.markForCheck();
  }

  private applyFilter(): void {
    this.visible = this.programs.filter(p => this.matchesTab(p, this.activeTab));
  }

  private matchesTab(p: ProgramDTO, tab: PromoTab): boolean {
    switch (tab) {
      case 'all':
        return true;
      case 'auto':
        return !!p.autoApply;
      case 'code':
        return !!p.linkedDiscountCode;
      case 'members':
        return p.eligibility === CinemaServiceAgent.PromotionEligibility.Members;
      case 'students':
        return p.eligibility === CinemaServiceAgent.PromotionEligibility.StudentOrTeacher;
    }
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

  /** Big numeral shown on the ticket stub, or null to fall back to an icon (PromotionMechanic.Info). */
  stubValue(p: ProgramDTO): string | null {
    if (p.mechanic === CinemaServiceAgent.PromotionMechanic.PercentOff) {
      return p.percentOff ? `-${p.percentOff}%` : null;
    }
    if (p.mechanic === CinemaServiceAgent.PromotionMechanic.FixedPrice) {
      const prices = (p.prices ?? []).map(x => x.price ?? 0).filter(x => x > 0);
      if (prices.length === 0) {
        return null;
      }
      return this._compactPrice(Math.min(...prices));
    }
    return null;
  }

  private _compactPrice(value: number): string {
    if (value < 1000) {
      return `${value}`;
    }
    const thousands = value / 1000;
    const rounded = Math.round(thousands * 10) / 10;
    const text = Number.isInteger(rounded) ? `${rounded}` : rounded.toFixed(1);
    return `${text}K`;
  }

  /** Short caption under the stub numeral: the recurring day(s) when uniform across schedules, else an eligibility hint. */
  stubCaption(p: ProgramDTO): string {
    const masks = new Set((p.schedules ?? []).map(s => s.daysOfWeekMask ?? 0));
    if (masks.size === 1) {
      const [mask] = masks;
      const days = daysMaskToList(mask);
      if (days.length > 0 && days.length < 7) {
        return this.dayLabelsText(mask);
      }
    }
    const key = {
      [CinemaServiceAgent.PromotionEligibility.Everyone]: 'promotions.stubCaption.everyone',
      [CinemaServiceAgent.PromotionEligibility.Members]: 'promotions.stubCaption.members',
      [CinemaServiceAgent.PromotionEligibility.StudentOrTeacher]: 'promotions.stubCaption.studentOrTeacher',
      [CinemaServiceAgent.PromotionEligibility.AgeLimit]: 'promotions.stubCaption.ageLimit',
    }[p.eligibility ?? CinemaServiceAgent.PromotionEligibility.Everyone] ?? 'promotions.stubCaption.everyone';
    return this._translate.instant(key);
  }

  /** "HH:mm – HH:mm" when every schedule shares the same time window, else null (ledger row is hidden). */
  scheduleWindow(p: ProgramDTO): string | null {
    const schedules = p.schedules ?? [];
    if (schedules.length === 0) {
      return null;
    }
    const starts = new Set(schedules.map(s => s.startTimeOfDay ?? ''));
    const ends = new Set(schedules.map(s => s.endTimeOfDay ?? ''));
    if (starts.size !== 1 || ends.size !== 1) {
      return null;
    }
    const [start] = starts;
    const [end] = ends;
    if (!start || !end) {
      return null;
    }
    return `${start} – ${end}`;
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
