import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, ViewChild, computed, effect, inject, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import {
  EmptyStateComponent,
  SharedModule,
  StaffServiceAgent,
  StatusPillComponent,
  scanOutcomeSpec,
  showException,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import {
  RecentScan,
  buildAgeConfirmRequest,
  buildScanRequest,
  isAdmitted,
  needsAgePrompt,
  normalizeScanCode,
  pushRecentScan,
  showsUsageDetails,
} from './gate-scan.state';

/** One showtime choice of the optional filter. */
interface ShowTimeOption {
  id: string;
  label: string;
}

/** Delay before a green "admitted" result clears itself so the next patron can be scanned. */
const ADMITTED_AUTO_CLEAR_MS = 2500;

/** How often the camera frame is sampled for a code. */
const CAMERA_SCAN_INTERVAL_MS = 300;

/**
 * Gate scan screen: an autofocused input that takes keyboard-wedge scanner input (submits on Enter), an optional
 * camera button (native BarcodeDetector), a big green / amber / red result and a recent scans list.
 * Rejections come back as HTTP 200 with an outcome, so they are shown here rather than by the error interceptor.
 */
@Component({
  selector: 'staff-gate-scan',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
@if (!theaterId()) {
  <mat-card class="ad-card--pad-0">
    <cl-empty-state icon="theaters" messageKey="gate.pickTheater" hintKey="gate.pickTheaterHint" />
  </mat-card>
} @else {
  <div class="scan-panel ad-card">
    <label class="scan-label" for="gate-scan-input">{{ 'gate.scanLabel' | translate }}</label>
    <div class="scan-row">
      <input #scanInput id="gate-scan-input" class="ad-input scan-input" type="text" autocomplete="off" autofocus
             [placeholder]="'gate.scanPlaceholder' | translate"
             [disabled]="busy()"
             (keydown.enter)="submit(scanInput.value); scanInput.value = ''">
      @if (cameraSupported) {
        <button mat-stroked-button type="button" (click)="toggleCamera()">
          <mat-icon>{{ cameraOn() ? 'videocam_off' : 'photo_camera' }}</mat-icon>
          {{ (cameraOn() ? 'gate.cameraStop' : 'gate.cameraStart') | translate }}
        </button>
      }
    </div>
    @if (cameraOn()) {
      <video #cameraVideo class="camera" autoplay playsinline muted></video>
    }
    @if (showTimes().length) {
      <mat-form-field appearance="outline" class="showtime-filter">
        <mat-label>{{ 'gate.showTimeFilter' | translate }}</mat-label>
        <mat-select [value]="showTimeId()" (selectionChange)="showTimeId.set($event.value)">
          <mat-option value="">{{ 'gate.anyShowTime' | translate }}</mat-option>
          @for (option of showTimes(); track option.id) {
            <mat-option [value]="option.id">{{ option.label }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    }
  </div>

  <h2 class="recent-title">{{ 'gate.recent' | translate }}</h2>
  @if (recent().length) {
    <div class="ad-card recent">
      @for (scan of recent(); track $index) {
        <div class="recent-row">
          <cl-status-pill kind="scanOutcome" [value]="scan.outcome" />
          <strong>{{ scan.seatLabel }}</strong>
          <span class="recent-movie">{{ scan.movieTitle }}</span>
          <span class="recent-time">{{ scan.at | date: 'HH:mm:ss' }}</span>
        </div>
      }
    </div>
  } @else {
    <mat-card class="ad-card--pad-0"><cl-empty-state icon="qr_code_scanner" messageKey="gate.recentEmpty" /></mat-card>
  }
}

@if (result(); as r) {
  <div class="overlay" [class]="'overlay tone-' + tone()" role="alertdialog" (keydown.escape)="dismiss()">
    <div class="overlay-body">
      <mat-icon class="overlay-icon">{{ icon() }}</mat-icon>
      <h1 class="overlay-title">{{ outcomeKey() | translate }}</h1>
      @if (r.seatLabel) {
        <div class="detail seat">{{ r.seatLabel }}</div>
        <div class="detail">{{ r.movieTitle }}</div>
        <div class="detail">{{ r.roomName }} &middot; {{ r.showTime | date: 'HH:mm dd/MM/yyyy' }}</div>
        @if (r.patronCategory) {
          <div class="detail">{{ r.patronCategory }}</div>
        }
        @if (r.ageRatingCode) {
          <div class="detail">{{ 'gate.ageRating' | translate: { code: r.ageRatingCode } }}</div>
        }
      }
      @if (usageDetails()) {
        <div class="detail used">{{ 'gate.usedAt' | translate: { by: r.usedBy ?? '-', at: (r.usedAt | date: 'HH:mm dd/MM/yyyy') } }}</div>
      }
      <div class="overlay-actions">
        @if (agePrompt()) {
          <p class="age-question">{{ 'gate.ageQuestion' | translate: { age: r.minAge ?? 0 } }}</p>
          <button mat-flat-button type="button" class="big-btn" (click)="confirmAge()">{{ 'gate.confirmAge' | translate }}</button>
          <button mat-stroked-button type="button" class="big-btn" (click)="dismiss()">{{ 'gate.denyAge' | translate }}</button>
        } @else {
          <button mat-flat-button type="button" class="big-btn" (click)="dismiss()">{{ 'gate.next' | translate }}</button>
        }
      </div>
    </div>
  </div>
}
`,
  styles: [`
    .scan-panel { display: flex; flex-direction: column; gap: 12px; padding: 24px; }
    .scan-label { font-weight: 600; color: var(--ml-ink); }
    .scan-row { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
    .scan-input { flex: 1 1 280px; font-size: 1.4rem; padding: 14px 16px; }
    .camera { width: 100%; max-width: 420px; border-radius: 8px; }
    .showtime-filter { max-width: 420px; }
    .recent-title { font-family: var(--ml-font-head); text-transform: uppercase; font-size: 1rem; margin: 24px 0 8px; color: var(--ml-ink); }
    .recent-row { display: flex; align-items: center; gap: 12px; padding: 8px 0; border-bottom: 1px solid var(--ml-panel-3, rgba(0, 0, 0, 0.08)); }
    .recent-movie { flex: 1; color: var(--ml-muted); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .recent-time { color: var(--ml-muted); font-variant-numeric: tabular-nums; }
    .overlay { position: fixed; inset: 0; z-index: 2000; display: flex; align-items: center; justify-content: center; text-align: center; color: #fff; padding: 24px; }
    .tone-success { background: #1b8a3a; }
    .tone-warn { background: #c77d00; }
    .tone-danger { background: #c62828; }
    .overlay-icon { font-size: 96px; width: 96px; height: 96px; }
    .overlay-title { font-size: 2.4rem; margin: 8px 0 24px; text-transform: uppercase; }
    .detail { font-size: 1.3rem; margin: 4px 0; }
    .detail.seat { font-size: 3rem; font-weight: 700; }
    .detail.used { font-weight: 700; }
    .overlay-actions { margin-top: 32px; display: flex; gap: 16px; justify-content: center; flex-wrap: wrap; }
    .age-question { width: 100%; font-size: 1.4rem; margin: 0; }
    .big-btn { min-width: 160px; min-height: 56px; font-size: 1.1rem; background: #fff; color: #222; }
  `],
})
export class GateScanComponent implements OnDestroy {
  private readonly _gate = inject(StaffServiceAgent.GateHttpService);
  private readonly _operations = inject(StaffServiceAgent.OperationsHttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);

  @ViewChild('scanInput') private _input?: ElementRef<HTMLInputElement>;
  @ViewChild('cameraVideo') private set _video(el: ElementRef<HTMLVideoElement> | undefined) {
    if (el && this._stream && el.nativeElement.srcObject !== this._stream) {
      el.nativeElement.srcObject = this._stream;
    }
  }

  readonly theaterId = this._theater.currentTheaterId;
  readonly busy = signal(false);
  readonly result = signal<StaffServiceAgent.ScanTicketResultDTO | null>(null);
  readonly recent = signal<RecentScan[]>([]);
  readonly showTimes = signal<ShowTimeOption[]>([]);
  readonly showTimeId = signal<string>('');
  readonly cameraOn = signal(false);

  readonly tone = computed(() => scanOutcomeSpec(this.result()?.outcome).tone);
  readonly outcomeKey = computed(() => scanOutcomeSpec(this.result()?.outcome).labelKey);
  readonly agePrompt = computed(() => needsAgePrompt(this.result()));
  readonly usageDetails = computed(() => showsUsageDetails(this.result()));
  readonly icon = computed(() => {
    switch (this.tone()) {
      case 'success': {
        return 'check_circle';
      }
      case 'warn': {
        return 'warning';
      }
      default: {
        return 'cancel';
      }
    }
  });

  readonly cameraSupported = typeof window !== 'undefined' && 'BarcodeDetector' in window
    && !!navigator.mediaDevices?.getUserMedia;

  private _lastCode = '';
  private _autoClear?: ReturnType<typeof setTimeout>;
  private _stream?: MediaStream;
  private _cameraTimer?: ReturnType<typeof setInterval>;

  constructor() {
    effect(() => {
      const theaterId = this.theaterId();
      this.showTimeId.set('');
      this._loadShowTimes(theaterId);
    });
    effect(() => {
      if (this.theaterId() && !this.result()) {
        setTimeout(() => this._focusInput());
      }
    });
  }

  ngOnDestroy(): void {
    this._stopCamera();
    if (this._autoClear) {
      clearTimeout(this._autoClear);
    }
  }

  /** Scans a code coming from the scanner input or the camera. */
  submit(raw: string): void {
    const code = normalizeScanCode(raw);
    if (!code || this.busy() || this.result()) {
      return;
    }
    this._lastCode = code;
    this._scan(buildScanRequest(code, this._options(false)));
  }

  /** Gate keeper confirmed the patron's age: re-scan the same code with ageConfirmed. */
  confirmAge(): void {
    const request = buildAgeConfirmRequest(this._lastCode, this.result(), this._options(true));
    if (!request) {
      return;
    }
    this.result.set(null);
    this._scan(request);
  }

  dismiss(): void {
    if (this._autoClear) {
      clearTimeout(this._autoClear);
      this._autoClear = undefined;
    }
    this.result.set(null);
  }

  toggleCamera(): void {
    if (this.cameraOn()) {
      this._stopCamera();
    } else {
      void this._startCamera();
    }
  }

  private _options(ageConfirmed: boolean) {
    return { theaterId: this.theaterId(), showTimeId: this.showTimeId(), ageConfirmed };
  }

  private _scan(request: StaffServiceAgent.ScanTicketRequest): void {
    this.busy.set(true);
    this._gate.scan(request).subscribe({
      next: result => {
        this.busy.set(false);
        this.result.set(result);
        this.recent.set(pushRecentScan(this.recent(), request.code, result, new Date()));
        this._cue(result);
        if (isAdmitted(result)) {
          this._autoClear = setTimeout(() => this.dismiss(), ADMITTED_AUTO_CLEAR_MS);
        }
      },
      error: error => {
        this.busy.set(false);
        this._store.dispatch(showException({ error }));
      },
    });
  }

  private _loadShowTimes(theaterId: string | null): void {
    if (!theaterId) {
      this.showTimes.set([]);
      return;
    }
    this._operations.getScheduleBoard(StaffServiceAgent.ScheduleBoardRequest.fromJS({ theaterId, date: new Date() })).subscribe({
      next: board => {
        const options: { id: string; label: string; start: number }[] = [];
        for (const room of board.rooms ?? []) {
          for (const showTime of room.showTimes ?? []) {
            if (showTime.showTimeId) {
              const start = showTime.start ? new Date(showTime.start) : new Date(0);
              const time = start.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
              options.push({ id: showTime.showTimeId, label: `${time} - ${showTime.movieTitle ?? ''} (${room.roomName ?? ''})`, start: start.getTime() });
            }
          }
        }
        options.sort((a, b) => a.start - b.start);
        this.showTimes.set(options.map(o => ({ id: o.id, label: o.label })));
      },
      // The filter is optional: if the board cannot be loaded the gate still scans without it.
      error: () => this.showTimes.set([]),
    });
  }

  private _focusInput(): void {
    this._input?.nativeElement.focus();
  }

  /** Sound and vibration cue; best effort, never blocks the scan. */
  private _cue(result: StaffServiceAgent.ScanTicketResultDTO): void {
    try {
      const tone = scanOutcomeSpec(result.outcome).tone;
      navigator.vibrate?.(tone === 'success' ? 80 : [200, 100, 200]);
      const audio = new AudioContext();
      const oscillator = audio.createOscillator();
      oscillator.frequency.value = tone === 'success' ? 880 : 220;
      oscillator.connect(audio.destination);
      oscillator.start();
      oscillator.stop(audio.currentTime + 0.15);
      oscillator.onended = () => void audio.close();
    } catch {
      /* no audio or vibration support: the visual result is enough */
    }
  }

  private async _startCamera(): Promise<void> {
    try {
      this._stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
      const Detector = (window as unknown as { BarcodeDetector: new (options?: { formats: string[] }) => { detect(source: CanvasImageSource): Promise<{ rawValue: string }[]> } }).BarcodeDetector;
      const detector = new Detector({ formats: ['qr_code'] });
      this.cameraOn.set(true);
      this._cameraTimer = setInterval(async () => {
        const video = document.querySelector<HTMLVideoElement>('video.camera');
        if (!video || this.busy() || this.result()) {
          return;
        }
        try {
          const codes = await detector.detect(video);
          if (codes.length) {
            this.submit(codes[0].rawValue);
          }
        } catch {
          /* frame not ready yet */
        }
      }, CAMERA_SCAN_INTERVAL_MS);
    } catch (error) {
      this._stopCamera();
      this._store.dispatch(showException({ error }));
    }
  }

  private _stopCamera(): void {
    if (this._cameraTimer) {
      clearInterval(this._cameraTimer);
      this._cameraTimer = undefined;
    }
    this._stream?.getTracks().forEach(track => track.stop());
    this._stream = undefined;
    this.cameraOn.set(false);
  }
}
