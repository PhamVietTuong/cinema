import * as QRCode from 'qrcode';

/** Sample booking used to fill a template's tokens in the live preview. Mirrors the keys produced by
 * BookingManager.BuildConfirmationValues (cinemabe) — keep the two in sync. */
const SAMPLE_VALUES: Record<string, string> = {
  CustomerName: 'Nguyễn Văn A',
  BookingCode: 'CIN202610171237528485',
  MovieTitle: 'Deadpool & Wolverine',
  TheaterName: 'Cinema Landmark 81',
  RoomName: 'Phòng 1',
  RoomTypeName: 'Midnight Premium',
  ProjectionForm: '2D',
  ShowDate: '18/10/2026',
  ShowStartTime: '06:45',
  ShowTime: '06:45 · 18/10/2026',
  TicketTypes: 'Người Cao Tuổi',
  SeatLabels: 'B3',
  TicketCount: '1',
  Concessions: '1 × Kẹo Dẻo Trái Cây',
  SubTotal: '160,000đ',
  DiscountAmount: '0đ',
  TotalAmount: '160,000đ',
  QrCodes: 'CIN202610171237528485-B3',
  TicketsUrl: '#',
  HomeUrl: '#',
};

const ICON_NAMES = ['check', 'clapper', 'ticket', 'home'];

function escapeHtml(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

/** Renders `body` with sample data. Like the server's TemplateRenderer, values are HTML-encoded for email
 * and `QrImages` is trusted markup; `cid:` inline-image references are pointed at assets the browser can load. */
export async function renderTemplatePreview(body: string, isEmail: boolean): Promise<string> {
  const qrDataUrl = isEmail && body.includes('{{QrImages}}')
    ? await QRCode.toDataURL(SAMPLE_VALUES['QrCodes'], { margin: 0, width: 152 })
    : '';
  const qrImages = qrDataUrl
    ? `<div style="display:inline-block;margin:4px 6px;text-align:center;"><div style="background:#ffffff;border-radius:12px;padding:12px;">` +
      `<img src="${qrDataUrl}" width="152" height="152" alt="QR B3" style="display:block;border:0;" /></div></div>`
    : '-';

  let html = body.replace(/\{\{\s*(\w+)\s*\}\}/g, (match, key: string) => {
    if (key === 'QrImages') {
      return qrImages;
    }
    const value = SAMPLE_VALUES[key];
    if (value === undefined) {
      return match;
    }
    return isEmail ? escapeHtml(value) : value;
  });

  for (const name of ICON_NAMES) {
    html = html.split(`cid:icon-${name}@cinema`).join(new URL(`assets/email-icons/${name}.png`, document.baseURI).href);
  }
  return html;
}
