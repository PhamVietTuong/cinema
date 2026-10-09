import { CinemaServiceAgent } from 'CinemaLib';

export function escapeHtml(value: string | null | undefined): string {
  return (value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/**
 * Printable document for reprinted tickets: one 80 mm page per ticket with its QR image.
 * `qrImages[i]` is the data URL for `result.tickets[i]` (empty string = no QR).
 */
export function buildTicketsPrintHtml(
  result: CinemaServiceAgent.ReprintResultDTO,
  qrImages: readonly string[],
  options: { title: string },
): string {
  const tickets = (result.tickets ?? []).map((ticket, index) => {
    const image = qrImages[index] ? `<img src="${escapeHtml(qrImages[index])}" width="160" height="160" alt="">` : '';
    return `<section class="ticket">
  <h2>${escapeHtml(result.movieTitle)}</h2>
  <p>${escapeHtml(result.roomName)}</p>
  <p class="seat">${escapeHtml(ticket.seatLabel)}</p>
  ${image}
  <p class="code">${escapeHtml(result.invoiceCode)}</p>
</section>`;
  }).join('\n');

  return `<!doctype html>
<html><head><meta charset="utf-8"><title>${escapeHtml(options.title)}</title>
<style>
  @page { size: 80mm auto; margin: 4mm; }
  body { font-family: sans-serif; margin: 0; text-align: center; }
  .ticket { page-break-after: always; padding: 4mm 0; }
  .ticket:last-child { page-break-after: auto; }
  h2 { font-size: 14px; margin: 0 0 4px; }
  p { margin: 2px 0; font-size: 12px; }
  .seat { font-size: 20px; font-weight: bold; }
  .code { font-family: monospace; }
</style></head><body>
${tickets}
</body></html>`;
}
