import {
  ChangeDetectionStrategy, Component, ElementRef, HostListener, computed, effect, inject, input, output, signal,
} from '@angular/core';
import { BreakpointObserver } from '@angular/cdk/layout';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormGroup } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { map } from 'rxjs/operators';
import { FilterBarField } from '../filter-bar/filter-bar.component';
import { MEDIA_TABLET_SMALL } from '../../utils/breakpoints';
import { toDateKey } from '../../utils/date-range';

/** A one-tap fill for the time-range pill (both ends are 'HH:mm'). */
export interface FilterPillsTimePreset {
  labelKey: string;
  from: string;
  to: string;
}

type PillKind = 'select' | 'date' | 'time';

export interface FilterPill {
  id: string;
  kind: PillKind;
  labelKey: string;
  icon: string;
  /** Form controls this pill edits (a time pill edits every `time` field as one range). */
  keys: string[];
  field?: FilterBarField;
  /** Human text of the applied value, '' when the pill has no value. */
  text: string;
}

interface MenuOption {
  value: string;
  text: string;
  hint?: string;
}

const pad = (n: number): string => String(n).padStart(2, '0');
/** Case- and accent-insensitive text for the in-menu search. */
const fold = (s: string): string => s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();

/** Option lists at least this long get a search box inside their menu. */
const SEARCHABLE_FROM = 12;
/** Width of the desktop popover (keep in sync with the scss). */
const MENU_WIDTH = 320;
/** At or below the shared `tablet-small` boundary menus become a bottom sheet. */
const SHEET_QUERY = MEDIA_TABLET_SMALL;
let nextId = 0;

/**
 * Inline "pill" filters: one rounded button per field and a menu per pill (popover on desktop, bottom sheet
 * on phones ending with an apply button that shows the live result count). An applied pill shows its
 * value and a clear (x). Driven by the same `FilterBarField[]` + `FormGroup` as `cl-filter-bar`;
 * `select`, `date` and `time` fields are supported (all `time` fields collapse into ONE range pill).
 *
 * Usage:
 * `<cl-filter-pills [form]="form" [fields]="fields" [icons]="{ language: 'language' }" [resultCount]="total"
 *   [showClear]="hasActive" (filtersChange)="reload()" (clear)="clearAll()" />`
 */
@Component({
  selector: 'cl-filter-pills',
  standalone: true,
  imports: [MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './filter-pills.component.html',
  styleUrl: './filter-pills.component.scss',
})
export class FilterPillsComponent {
  private readonly _host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly _translate = inject(TranslateService);
  private readonly _breakpoints = inject(BreakpointObserver);

  readonly form = input.required<FormGroup>();
  readonly fields = input.required<readonly FilterBarField[]>();
  /** Material icon per field key (a time pill is keyed `time`). */
  readonly icons = input<Record<string, string>>({});
  /** i18n key of the time-range pill label. */
  readonly timeLabelKey = input<string>('clFilterPills.time');
  readonly timePresets = input<readonly FilterPillsTimePreset[]>([
    { labelKey: 'clFilterPills.morning', from: '06:00', to: '12:00' },
    { labelKey: 'clFilterPills.afternoon', from: '12:00', to: '18:00' },
    { labelKey: 'clFilterPills.evening', from: '18:00', to: '23:59' },
  ]);
  /** Live total shown on the sheet's apply button; null/undefined = unknown (plain "Done"). */
  readonly resultCount = input<number | null | undefined>(null);
  /** Shows the trailing clear-all link. */
  readonly showClear = input<boolean>(false);
  readonly clearLabelKey = input<string>('clFilterPills.clearAll');
  /** Emitted after the user changes any value. */
  readonly filtersChange = output<void>();
  /** Emitted when the clear-all link is pressed (the page owns what "all" means). */
  readonly clear = output<void>();

  readonly menuId = `cfp-menu-${nextId++}`;
  readonly openId = signal<string | null>(null);
  readonly alignRight = signal(false);
  readonly menuQuery = signal('');
  readonly sheet = toSignal(this._breakpoints.observe(SHEET_QUERY).pipe(map(s => s.matches)), { initialValue: false });

  /** Bumped when the form value or the UI language changes, so `pills` recomputes. */
  private readonly _tick = signal(0);
  private _lastTrigger: HTMLElement | null = null;

  readonly pills = computed<FilterPill[]>(() => {
    this._tick();
    const raw = this.form().getRawValue() as Record<string, string | null>;
    const icons = this.icons();
    const out: FilterPill[] = [];
    let timePill: FilterPill | null = null;
    for (const f of this.fields()) {
      if (f.type === 'time') {
        if (!timePill) {
          timePill = { id: 'time', kind: 'time', labelKey: this.timeLabelKey(), icon: icons['time'] ?? 'schedule', keys: [], text: '' };
          out.push(timePill);
        }
        timePill.keys.push(f.key);
      } else if (f.type === 'select' || f.type === 'date') {
        out.push({
          id: f.key, kind: f.type, labelKey: f.labelKey, field: f, keys: [f.key],
          icon: icons[f.key] ?? (f.type === 'date' ? 'event' : 'tune'),
          text: f.type === 'date' ? this._dateText(raw[f.key]) : this._optionText(f, raw[f.key]),
        });
      }
    }
    if (timePill) {
      const [from, to] = timePill.keys.map(k => raw[k] ?? '');
      timePill.text = from || to ? `${from || '00:00'} – ${to || '23:59'}` : '';
    }
    return out;
  });

  readonly options = computed<MenuOption[]>(() => {
    const p = this.pills().find(x => x.id === this.openId());
    if (!p || p.kind !== 'select') { return []; }
    const q = fold(this.menuQuery().trim());
    return (p.field?.options ?? [])
      .map(o => ({ value: o.value, text: o.labelKey ? this._translate.instant(o.labelKey) : (o.label ?? o.value) }))
      .filter(o => !q || fold(o.text).includes(q));
  });

  readonly searchable = computed(() => {
    const p = this.pills().find(x => x.id === this.openId());
    return (p?.field?.options?.length ?? 0) >= SEARCHABLE_FROM;
  });

  /** The next 7 days as quick date choices (re-evaluated every time a menu opens). */
  readonly dateOptions = computed<MenuOption[]>(() => {
    this.openId();
    const lang = this._translate.currentLang() || 'vi';
    const out: MenuOption[] = [];
    for (let i = 0; i < 7; i++) {
      const d = new Date();
      d.setDate(d.getDate() + i);
      let label: string;
      if (i === 0) {
        label = this._translate.instant('clFilterPills.today');
      } else if (i === 1) {
        label = this._translate.instant('clFilterPills.tomorrow');
      } else {
        label = new Intl.DateTimeFormat(lang.startsWith('en') ? 'en-US' : 'vi-VN', { weekday: 'short' }).format(d);
      }
      out.push({ value: toDateKey(d), text: label, hint: `${pad(d.getDate())}/${pad(d.getMonth() + 1)}` });
    }
    return out;
  });

  constructor() {
    effect(onCleanup => {
      const valueSub = this.form().valueChanges.subscribe(() => this._bump());
      const langSub = this._translate.onLangChange.subscribe(() => this._bump());
      onCleanup(() => { valueSub.unsubscribe(); langSub.unsubscribe(); });
    });
    // Background scroll is locked while the bottom sheet is open.
    effect(onCleanup => {
      if (this.sheet() && this.openId()) {
        const root = document.documentElement;
        const prev = root.style.overflow;
        root.style.overflow = 'hidden';
        // Hook for the host app: floating widgets (chat FAB...) can hide themselves while the sheet is open.
        root.classList.add('cfp-sheet-open');
        onCleanup(() => { root.style.overflow = prev; root.classList.remove('cfp-sheet-open'); });
      }
    });
  }

  // ── value helpers ────────────────────────────────────────────────────────
  valueOf(key: string): string {
    return (this.form().get(key)?.value as string | null) ?? '';
  }

  isSelected(p: FilterPill, value: string): boolean {
    return this.valueOf(p.keys[0]) === value;
  }

  isPreset(p: FilterPill, preset: FilterPillsTimePreset): boolean {
    return this.valueOf(p.keys[0]) === preset.from && this.valueOf(p.keys[1] ?? '') === preset.to;
  }

  private _bump(): void { this._tick.update(n => n + 1); }

  private _dateText(v: string | null | undefined): string {
    if (!v) { return ''; }
    const today = new Date();
    const tomorrow = new Date();
    tomorrow.setDate(today.getDate() + 1);
    if (v === toDateKey(today)) { return this._translate.instant('clFilterPills.today'); }
    if (v === toDateKey(tomorrow)) { return this._translate.instant('clFilterPills.tomorrow'); }
    const [y, m, d] = v.split('-');
    return y && m && d ? `${d}/${m}/${y}` : v;
  }

  private _optionText(f: FilterBarField, v: string | null | undefined): string {
    if (!v) { return ''; }
    const o = (f.options ?? []).find(x => x.value === v);
    if (!o) { return ''; }
    return o.labelKey ? this._translate.instant(o.labelKey) : (o.label ?? o.value);
  }

  private _commit(): void {
    this.form().markAsDirty();
    this._bump();
    this.filtersChange.emit();
  }

  // ── menu open/close ──────────────────────────────────────────────────────
  toggle(p: FilterPill, ev: Event): void {
    if (this.openId() === p.id) {
      this.closeMenu(true);
      return;
    }
    this.openMenu(p, ev.currentTarget as HTMLElement);
  }

  openMenu(p: FilterPill, trigger: HTMLElement): void {
    this._lastTrigger = trigger;
    this.menuQuery.set('');
    const rect = trigger.getBoundingClientRect();
    this.alignRight.set(rect.left + MENU_WIDTH > window.innerWidth - 12);
    this.openId.set(p.id);
    // Wait for the menu to render, then move focus in. A sheet's search box is skipped so the keyboard does not pop up.
    setTimeout(() => {
      const root = this._menuEl();
      const search = this.sheet() ? null : root?.querySelector<HTMLElement>('input[type="search"]');
      const target = search
        ?? root?.querySelector<HTMLElement>('[role="option"][aria-selected="true"]')
        ?? root?.querySelector<HTMLElement>('[role="option"]')
        ?? root?.querySelector<HTMLElement>('.cfp-focusable');
      target?.focus();
    });
  }

  closeMenu(returnFocus = false): void {
    if (this.openId() === null) { return; }
    this.openId.set(null);
    if (returnFocus) { this._lastTrigger?.focus(); }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(ev: Event): void {
    if (this.openId() !== null && !this._host.nativeElement.contains(ev.target as Node)) {
      this.closeMenu();
    }
  }

  onPillKey(ev: KeyboardEvent, p: FilterPill): void {
    if (ev.key === 'ArrowDown' && this.openId() !== p.id) {
      ev.preventDefault();
      this.openMenu(p, ev.currentTarget as HTMLElement);
    }
  }

  onMenuKey(ev: KeyboardEvent): void {
    if (ev.key === 'Escape') {
      ev.preventDefault();
      ev.stopPropagation();
      this.closeMenu(true);
      return;
    }
    if (ev.key !== 'ArrowDown' && ev.key !== 'ArrowUp' && ev.key !== 'Home' && ev.key !== 'End') { return; }
    const active = document.activeElement as HTMLElement | null;
    // Home/End and the arrows keep their native meaning inside a text/date/time input (the search box excepted for arrows).
    if (active?.tagName === 'INPUT' && (active as HTMLInputElement).type !== 'search') { return; }
    if (active?.tagName === 'INPUT' && (ev.key === 'Home' || ev.key === 'End')) { return; }
    const items = Array.from(this._menuEl()?.querySelectorAll<HTMLElement>('[role="option"], .cfp-focusable') ?? []);
    if (!items.length) { return; }
    const cur = active ? items.indexOf(active) : -1;
    let next: number;
    if (ev.key === 'ArrowDown') {
      next = cur < 0 ? 0 : Math.min(items.length - 1, cur + 1);
    } else if (ev.key === 'ArrowUp') {
      next = cur < 0 ? items.length - 1 : Math.max(0, cur - 1);
    } else if (ev.key === 'Home') {
      next = 0;
    } else {
      next = items.length - 1;
    }
    ev.preventDefault();
    items[next].focus();
  }

  private _menuEl(): HTMLElement | null {
    return this._host.nativeElement.querySelector<HTMLElement>('.cfp-menu');
  }

  // ── value changes ────────────────────────────────────────────────────────
  /** Picks a choice; picking the applied one again clears it (when the field allows an empty value). */
  pick(p: FilterPill, value: string): void {
    const key = p.keys[0];
    const same = this.valueOf(key) === value;
    const clearable = p.field?.allowEmpty !== false;
    this.form().get(key)?.setValue(same && clearable ? '' : value);
    this._commit();
    // A popover closes at once; the phone sheet stays open so the apply button's count can be read first.
    if (!this.sheet()) { this.closeMenu(true); }
  }

  setCustom(p: FilterPill, ev: Event): void {
    this.form().get(p.keys[0])?.setValue((ev.target as HTMLInputElement).value);
    this._commit();
  }

  setTimePart(p: FilterPill, key: string, ev: Event): void {
    this.form().get(key)?.setValue((ev.target as HTMLInputElement).value);
    this._commit();
  }

  applyPreset(p: FilterPill, preset: FilterPillsTimePreset): void {
    const off = this.isPreset(p, preset);
    this.form().get(p.keys[0])?.setValue(off ? '' : preset.from);
    this.form().get(p.keys[1])?.setValue(off ? '' : preset.to);
    this._commit();
  }

  clearPill(p: FilterPill): void {
    for (const k of p.keys) { this.form().get(k)?.setValue(''); }
    if (this.openId() === p.id) { this.closeMenu(); }
    this._commit();
  }

  onSearch(ev: Event): void {
    this.menuQuery.set((ev.target as HTMLInputElement).value);
  }
}
