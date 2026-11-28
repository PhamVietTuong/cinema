import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { KpiCardComponent, KpiCardVariant } from './kpi-card.component';

@Component({
  standalone: true,
  imports: [KpiCardComponent],
  template: `<cl-kpi-card label="Revenue today" [value]="value" [icon]="icon" [variant]="variant" />`,
})
class HostComponent {
  value: string | number | null = 1200;
  icon: string | null = null;
  variant: KpiCardVariant = 'default';
}

describe('KpiCardComponent', () => {
  const setup = (configure: (host: HostComponent) => void = () => {}) => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    const fixture = TestBed.createComponent(HostComponent);
    configure(fixture.componentInstance);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('shows the label and the figure', () => {
    const el = setup();
    expect(el.querySelector('.kpi-label')?.textContent).toContain('Revenue today');
    expect(el.querySelector('.kpi-value')?.textContent).toContain('1200');
    expect(el.querySelector('.ad-card')).not.toBeNull();
  });

  it('puts the icon beside the label in the stub variant', () => {
    const el = setup(h => { h.variant = 'stub'; h.icon = 'movie'; });
    expect(el.querySelector('.kpi--stub')).not.toBeNull();
    expect(el.querySelector('.kpi-head .kpi-icon mat-icon')?.textContent).toContain('movie');
  });

  it('omits the icon chip when no icon is given', () => {
    const el = setup(h => { h.variant = 'stub'; });
    expect(el.querySelector('.kpi-icon')).toBeNull();
  });

  it('marks the compact variant', () => {
    const el = setup(h => { h.variant = 'compact'; });
    expect(el.querySelector('.kpi--compact')).not.toBeNull();
    expect(el.querySelector('.kpi-head')).toBeNull();
  });

  it('renders nothing for a missing value instead of "null"', () => {
    const el = setup(h => { h.value = null; });
    expect(el.querySelector('.kpi-value')?.textContent?.trim()).toBe('');
  });
});
