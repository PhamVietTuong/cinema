import { Component } from '@angular/core';
import { SharedModule } from 'CinemaLib';

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
  open = false;
  input = '';
  messages: ChatMsg[] = [];

  readonly quickReplies = ['Đặt vé thế nào?', 'Giờ chiếu', 'Hủy / hoàn vé', 'Ưu đãi thành viên', 'Rạp gần tôi'];

  private readonly faqs: { k: string[]; a: string }[] = [
    { k: ['đặt vé', 'mua vé', 'booking', 'đặt'], a: 'Chọn phim → suất chiếu → ghế → thanh toán. Vé điện tử (mã QR) sẽ nằm trong "Tài khoản → Lịch Sử Đặt Vé".' },
    { k: ['giờ', 'suất', 'lịch chiếu', 'showtime'], a: 'Lịch chiếu hiển thị trong trang chi tiết từng phim, nhóm theo định dạng 2D/3D/IMAX/4DX.' },
    { k: ['hủy', 'hoàn', 'refund', 'cancel'], a: 'Bạn có thể hủy vé khi đơn còn ở trạng thái "Chờ Thanh Toán" trong mục Lịch Sử Đặt Vé.' },
    { k: ['thành viên', 'điểm', 'tích điểm', 'membership', 'voucher', 'mã giảm', 'khuyến mãi'], a: 'Mỗi 10.000đ chi tiêu được 1 điểm; hạng thành viên tự nâng theo điểm. Nhập mã giảm giá ở bước thanh toán.' },
    { k: ['rạp', 'gần', 'địa chỉ', 'theater', 'vị trí'], a: 'Vào trang "Rạp Chiếu" và bấm "Tìm rạp gần nhất" để sắp xếp theo khoảng cách tới bạn.' },
    { k: ['thanh toán', 'payment', 'momo', 'thẻ', 'ví'], a: 'Hỗ trợ thẻ nội địa và ví điện tử (MoMo / Apple Pay / Google Pay) ở bước thanh toán.' },
    { k: ['combo', 'bắp', 'nước', 'đồ ăn'], a: 'Bạn có thể thêm bắp nước & combo ngay ở bước thanh toán, tính gộp vào đơn.' },
  ];
  private readonly fallback =
    'Mình chưa hiểu ý bạn. Thử hỏi về: đặt vé, giờ chiếu, hủy/hoàn vé, thành viên, thanh toán, hoặc rạp gần bạn nhé.';

  toggle(): void {
    this.open = !this.open;
    if (this.open && !this.messages.length) {
      this._bot('Xin chào! Mình là trợ lý CINEMA. Mình có thể giúp gì cho bạn?');
    }
  }

  send(text?: string): void {
    const t = (text ?? this.input).trim();
    if (!t) { return; }
    this.messages.push({ from: 'user', text: t });
    this.input = '';
    this._bot(this._answer(t));
  }

  private _answer(text: string): string {
    const low = text.toLowerCase();
    return this.faqs.find(f => f.k.some(k => low.includes(k)))?.a ?? this.fallback;
  }
  private _bot(text: string): void {
    this.messages.push({ from: 'bot', text });
  }
}
