import { Component, inject } from '@angular/core';
import { SharedModule } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';

interface ChatMsg { from: 'bot' | 'user'; text: string; }

/**
 * Lightweight rule-based support chatbot (keyword → canned answer). No backend/LLM;
 * a real LLM or live-agent backend can be swapped in behind the same UI later.
 */
@Component({
  selector: 'app-chatbot',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './chatbot.component.html',
  styleUrl: './chatbot.component.scss',
})
export class ChatbotComponent {
  private _translate = inject(TranslateService);

  open = false;
  input = '';
  messages: ChatMsg[] = [];

  /** Quick-reply chips: `label` is displayed (translated); `q` is the Vietnamese phrase used for keyword matching. */
  readonly quickReplies: { label: string; q: string }[] = [
    { label: 'chatbot.quickBooking', q: 'Đặt vé thế nào?' },
    { label: 'chatbot.quickShowtimes', q: 'Giờ chiếu' },
    { label: 'chatbot.quickCancel', q: 'Hủy / hoàn vé' },
    { label: 'chatbot.quickMembership', q: 'Ưu đãi thành viên' },
    { label: 'chatbot.quickNearby', q: 'Rạp gần tôi' },
  ];

  /** `k` = Vietnamese keywords matched against user input (NOT translated); `a` = translation key for the answer. */
  private readonly faqs: { k: string[]; a: string }[] = [
    { k: ['đặt vé', 'mua vé', 'booking', 'đặt'], a: 'chatbot.answerBooking' },
    { k: ['giờ', 'suất', 'lịch chiếu', 'showtime'], a: 'chatbot.answerShowtimes' },
    { k: ['hủy', 'hoàn', 'refund', 'cancel'], a: 'chatbot.answerCancel' },
    { k: ['thành viên', 'điểm', 'tích điểm', 'membership', 'voucher', 'mã giảm', 'khuyến mãi'], a: 'chatbot.answerMembership' },
    { k: ['rạp', 'gần', 'địa chỉ', 'theater', 'vị trí'], a: 'chatbot.answerNearby' },
    { k: ['thanh toán', 'payment', 'momo', 'thẻ', 'ví'], a: 'chatbot.answerPayment' },
    { k: ['combo', 'bắp', 'nước', 'đồ ăn'], a: 'chatbot.answerCombo' },
  ];
  private readonly fallback = 'chatbot.fallback';

  toggle(): void {
    this.open = !this.open;
    if (this.open && !this.messages.length) {
      this._bot('chatbot.greeting');
    }
  }

  /** Sends a message. `text` is what appears in the user bubble; `matchText` (if given) is what the bot matches against. */
  send(text?: string, matchText?: string): void {
    const t = (text ?? this.input).trim();
    if (!t) { return; }
    this.messages.push({ from: 'user', text: t });
    this.input = '';
    this._bot(this._answer(matchText ?? t));
  }

  sendQuick(chip: { label: string; q: string }): void {
    this.send(this._translate.instant(chip.label), chip.q);
  }

  /** Returns the translation key for the best-matching FAQ answer (or the fallback key). */
  private _answer(text: string): string {
    const low = text.toLowerCase();
    return this.faqs.find(f => f.k.some(k => low.includes(k)))?.a ?? this.fallback;
  }
  private _bot(key: string): void {
    this.messages.push({ from: 'bot', text: this._translate.instant(key) });
  }
}
