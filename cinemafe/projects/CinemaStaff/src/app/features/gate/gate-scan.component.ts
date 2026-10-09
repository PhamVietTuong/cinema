import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, ViewChild, computed, effect, inject, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import {
  EmptyStateComponent,
  SharedModule,
  CinemaServiceAgent,
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
  templateUrl: './gate-scan.component.html',
  styleUrl: './gate-scan.component.scss',
})
export class GateScanComponent implements OnDestroy {
  private readonly _gate = inject(CinemaServiceAgent.HttpService);
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
  readonly result = signal<CinemaServiceAgent.ScanTicketResultDTO | null>(null);
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

  private _scan(request: CinemaServiceAgent.ScanTicketRequest): void {
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
    this._gate.getScheduleBoard(CinemaServiceAgent.ScheduleBoardRequest.fromJS({ theaterId, date: new Date() })).subscribe({
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
  private _cue(result: CinemaServiceAgent.ScanTicketResultDTO): void {
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
