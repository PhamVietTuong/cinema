import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { ChatbotComponent } from './chatbot.component';

/**
 * The test DOM has no layout, so scrollHeight/scrollTop are faked on `.cb-body`: scrollHeight grows with
 * the number of messages and scrollTop is recorded, which is enough to prove the component scrolls to the end.
 */
describe('ChatbotComponent scrolling', () => {
  const tops = new WeakMap<Element, number>();
  const heightOf = (el: Element) => el.querySelectorAll('.cb-msg').length * 100;
  const isBody = (el: Element) => el.classList.contains('cb-body');
  const proto = HTMLElement.prototype;

  beforeEach(() => {
    Object.defineProperty(proto, 'scrollHeight', {
      configurable: true,
      get(this: HTMLElement) { return isBody(this) ? heightOf(this) : 0; },
    });
    Object.defineProperty(proto, 'scrollTop', {
      configurable: true,
      get(this: HTMLElement) { return tops.get(this) ?? 0; },
      set(this: HTMLElement, v: number) { tops.set(this, v); },
    });
  });
  afterEach(() => {
    delete (proto as unknown as Record<string, unknown>)['scrollHeight'];
    delete (proto as unknown as Record<string, unknown>)['scrollTop'];
  });

  const setup = () => {
    TestBed.configureTestingModule({
      imports: [ChatbotComponent],
      providers: [provideTranslateService({ fallbackLang: 'vi' })],
    });
    const fixture = TestBed.createComponent(ChatbotComponent);
    fixture.detectChanges();
    return fixture;
  };
  type Fixture = ReturnType<typeof setup>;
  const root = (f: Fixture) => f.nativeElement as HTMLElement;
  const body = (f: Fixture) => root(f).querySelector('.cb-body') as HTMLElement;
  // Real DOM clicks, like a user: the event listener marks the view dirty under zoneless change detection.
  const clickFab = async (f: Fixture) => {
    (root(f).querySelector('.cb-fab') as HTMLElement).click();
    await f.whenStable();
  };
  const clickChip = async (f: Fixture, i: number) => {
    (root(f).querySelectorAll('.cb-chip')[i] as HTMLElement).click();
    await f.whenStable();
  };

  it('scrolls to the newest message when opened and after every user and bot message', async () => {
    const f = setup();
    await clickFab(f);
    expect(body(f).scrollTop).toBe(heightOf(body(f)));
    for (const i of [2, 1, 3]) {
      await clickChip(f, i);
      expect(heightOf(body(f))).toBeGreaterThan(100);
      expect(body(f).scrollTop).toBe(heightOf(body(f)));
    }
  });

  it('scrolls to the bottom again when the widget is reopened', async () => {
    const f = setup();
    await clickFab(f);
    await clickChip(f, 2);
    await clickFab(f); // close
    await clickFab(f); // reopen: the panel is rebuilt, so scrollTop starts at 0
    expect(heightOf(body(f))).toBeGreaterThan(0);
    expect(body(f).scrollTop).toBe(heightOf(body(f)));
  });
});
