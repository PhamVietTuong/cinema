import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { EmptyStateComponent } from './empty-state.component';

@Component({
  standalone: true,
  imports: [EmptyStateComponent],
  template: `<cl-empty-state [icon]="icon" messageKey="msg.key" [hintKey]="hintKey" [hintText]="hintText"
                             [illustrated]="illustrated" [ctaKey]="ctaKey" [ctaLink]="ctaLink" (cta)="clicks = clicks + 1" />`,
})
class HostComponent {
  icon = 'movie';
  hintKey: string | null = null;
  hintText: string | null = null;
  illustrated = false;
  ctaKey: string | null = null;
  ctaLink: string | null = null;
  clicks = 0;
}

describe('EmptyStateComponent', () => {
  const setup = (configure: (host: HostComponent) => void = () => {}) => {
    TestBed.configureTestingModule({ imports: [HostComponent], providers: [provideTranslateService({ fallbackLang: 'vi' }), provideRouter([])] });
    const fixture = TestBed.createComponent(HostComponent);
    configure(fixture.componentInstance);
    fixture.detectChanges();
    return { host: fixture.componentInstance, el: fixture.nativeElement as HTMLElement };
  };

  it('renders the plain .ad-empty markup by default (no illustration, no button)', () => {
    const { el } = setup();
    expect(el.querySelector('.ad-empty')).not.toBeNull();
    expect(el.querySelector('.es')).toBeNull();
    expect(el.querySelector('button, a')).toBeNull();
  });

  it('shows the hint, preferring verbatim hintText over hintKey', () => {
    const { el } = setup(h => { h.hintKey = 'hint.key'; h.hintText = 'Server said no'; });
    expect(el.querySelector('.cl-empty-hint')?.textContent).toContain('Server said no');
  });

  it('renders the illustrated variant with a title and the SVG badge', () => {
    const { el } = setup(h => { h.illustrated = true; });
    expect(el.querySelector('.es')).not.toBeNull();
    expect(el.querySelector('.es-svg')).not.toBeNull();
    expect(el.querySelector('.es-title')?.textContent).toContain('msg.key');
    expect(el.querySelector('.ad-empty')).toBeNull();
  });

  it('emits cta from the button when there is no link', () => {
    const { host, el } = setup(h => { h.illustrated = true; h.ctaKey = 'cta.key'; });
    el.querySelector<HTMLButtonElement>('button.es-cta')!.click();
    expect(host.clicks).toBe(1);
  });

  it('renders a router link instead of a button when ctaLink is set', () => {
    const { el } = setup(h => { h.illustrated = true; h.ctaKey = 'cta.key'; h.ctaLink = '/movies'; });
    expect(el.querySelector('a.es-cta')?.getAttribute('href')).toBe('/movies');
    expect(el.querySelector('button.es-cta')).toBeNull();
  });
});
