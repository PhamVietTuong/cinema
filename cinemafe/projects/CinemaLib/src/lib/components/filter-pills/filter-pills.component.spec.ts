import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { BreakpointObserver } from '@angular/cdk/layout';
import { provideTranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { FilterBarField } from '../filter-bar/filter-bar.component';
import { FilterPillsComponent } from './filter-pills.component';

const FIELDS: FilterBarField[] = [
  { key: 'genre', type: 'select', labelKey: 'g', options: [{ value: 'a', label: 'Action' }, { value: 'b', label: 'Drama' }] },
  { key: 'date', type: 'date', labelKey: 'd' },
  { key: 'from', type: 'time', labelKey: 'f' },
  { key: 'to', type: 'time', labelKey: 't' },
];

@Component({
  standalone: true,
  imports: [FilterPillsComponent],
  template: `<cl-filter-pills [form]="form" [fields]="fields" [resultCount]="count()" [showClear]="true"
                              (filtersChange)="changes = changes + 1" (clear)="cleared = cleared + 1" />`,
})
class HostComponent {
  form = new FormGroup({ genre: new FormControl(''), date: new FormControl(''), from: new FormControl(''), to: new FormControl('') });
  fields = FIELDS;
  count = signal<number | null>(42);
  changes = 0;
  cleared = 0;
}

describe('FilterPillsComponent', () => {
  const setup = (sheet = false) => {
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        provideTranslateService({ fallbackLang: 'vi' }),
        { provide: BreakpointObserver, useValue: { observe: () => of({ matches: sheet, breakpoints: {} }) } },
      ],
    });
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    return { fixture, host: fixture.componentInstance, el };
  };
  const pill = (el: HTMLElement, id: string) => el.querySelector<HTMLButtonElement>(`[data-pill="${id}"]`)!;

  it('renders one pill per select/date field and ONE pill for all time fields', () => {
    const { el } = setup();
    expect(Array.from(el.querySelectorAll('[data-pill]')).map(b => b.getAttribute('data-pill'))).toEqual(['genre', 'date', 'time']);
  });

  it('opens a menu with aria-expanded/aria-controls and picks a value, then closes it', () => {
    const { fixture, host, el } = setup();
    pill(el, 'genre').click();
    fixture.detectChanges();
    expect(pill(el, 'genre').getAttribute('aria-expanded')).toBe('true');
    const menu = el.querySelector('.cfp-menu')!;
    expect(pill(el, 'genre').getAttribute('aria-controls')).toBe(menu.id);
    const options = el.querySelectorAll<HTMLButtonElement>('[role="option"]');
    expect(options.length).toBe(2);

    options[1].click();
    fixture.detectChanges();
    expect(host.form.value.genre).toBe('b');
    expect(host.changes).toBe(1);
    expect(el.querySelector('.cfp-menu')).toBeNull();
    // applied pill shows its value
    expect(pill(el, 'genre').textContent).toContain('Drama');
  });

  it('picking the applied option again clears it', () => {
    const { fixture, host, el } = setup();
    host.form.controls.genre.setValue('a');
    pill(el, 'genre').click();
    fixture.detectChanges();
    el.querySelectorAll<HTMLButtonElement>('[role="option"]')[0].click();
    fixture.detectChanges();
    expect(host.form.value.genre).toBe('');
  });

  it('the x on an applied pill clears every control of that pill (time pill = both ends) and notifies', () => {
    const { fixture, host, el } = setup();
    host.form.patchValue({ from: '18:00', to: '22:00' });
    fixture.detectChanges();
    expect(pill(el, 'time').textContent).toContain('18:00 – 22:00');
    el.querySelector<HTMLButtonElement>('.cfp-item.on .cfp-x')!.click();
    fixture.detectChanges();
    expect(host.form.value.from).toBe('');
    expect(host.form.value.to).toBe('');
    expect(host.changes).toBe(1);
  });

  it('a time preset fills both ends and toggles off when applied again', () => {
    const { fixture, host, el } = setup();
    pill(el, 'time').click();
    fixture.detectChanges();
    const chips = el.querySelectorAll<HTMLButtonElement>('.cfp-chip');
    chips[2].click();
    fixture.detectChanges();
    expect(host.form.value.from).toBe('18:00');
    expect(host.form.value.to).toBe('23:59');
    chips[2].click();
    fixture.detectChanges();
    expect(host.form.value.from).toBe('');
    expect(host.form.value.to).toBe('');
  });

  it('Escape closes the menu and returns focus to the pill', () => {
    const { fixture, el } = setup();
    document.body.appendChild(el);
    pill(el, 'date').click();
    fixture.detectChanges();
    el.querySelector('.cfp-menu')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(el.querySelector('.cfp-menu')).toBeNull();
    expect(pill(el, 'date').getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(pill(el, 'date'));
    el.remove();
  });

  it('ArrowDown on a closed pill opens its menu', () => {
    const { fixture, el } = setup();
    pill(el, 'genre').dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    fixture.detectChanges();
    expect(el.querySelector('.cfp-menu')).not.toBeNull();
  });

  it('the clear link emits clear; the controls themselves are left to the page', () => {
    const { fixture, host, el } = setup();
    el.querySelector<HTMLButtonElement>('.cfp-clear')!.click();
    fixture.detectChanges();
    expect(host.cleared).toBe(1);
  });

  it('a click outside closes an open menu', () => {
    const { fixture, el } = setup();
    pill(el, 'genre').click();
    fixture.detectChanges();
    document.body.click();
    fixture.detectChanges();
    expect(el.querySelector('.cfp-menu')).toBeNull();
  });

  it('on a phone the menu is a sheet: stays open after a pick and its apply button shows the live count', () => {
    const { fixture, host, el } = setup(true);
    pill(el, 'genre').click();
    fixture.detectChanges();
    el.querySelectorAll<HTMLButtonElement>('[role="option"]')[0].click();
    fixture.detectChanges();
    expect(el.querySelector('.cfp-menu')).not.toBeNull();
    expect(el.querySelector('.cfp-menu')!.getAttribute('aria-modal')).toBe('true');
    // translate keys fall through untranslated in the test; the count is passed as a parameter
    expect(el.querySelector('.cfp-apply')!.textContent!.trim()).toContain('clFilterPills.applyCount');
    host.count.set(null);
    fixture.detectChanges();
    expect(el.querySelector('.cfp-apply')!.textContent!.trim()).toContain('clFilterPills.apply');
    expect(el.querySelector('.cfp-apply')!.textContent!.trim()).not.toContain('applyCount');
    el.querySelector<HTMLButtonElement>('.cfp-apply')!.click();
    fixture.detectChanges();
    expect(el.querySelector('.cfp-menu')).toBeNull();
  });

  it('reflects programmatic form changes (parent reset) in the pills', () => {
    const { fixture, host, el } = setup();
    host.form.controls.genre.setValue('a');
    fixture.detectChanges();
    expect(pill(el, 'genre').textContent).toContain('Action');
    host.form.reset({ genre: '', date: '', from: '', to: '' });
    fixture.detectChanges();
    expect(pill(el, 'genre').textContent).not.toContain('Action');
  });
});
