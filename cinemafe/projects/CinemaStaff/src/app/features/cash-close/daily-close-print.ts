import { CinemaServiceAgent } from 'CinemaLib';

function escapeHtml(value: string | number | null | undefined): string {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export interface DailyClosePrintLabels {
  title: string;
  heading: string;
  tenders: string;
  tender: string;
  amount: string;
  count: string;
  summary: Record<'paymentsTotal' | 'moneyCollected' | 'refunds' | 'exchanges' | 'comps' | 'netCollected' | 'tickets' | 'foodItems' | 'foodRevenue' | 'totalVariance', string>;
  /** Translated tender name for a PaymentTender. */
  tenderName: (tender?: CinemaServiceAgent.PaymentTender) => string;
  /** Formats a money amount. */
  money: (value?: number) => string;
}

/** Printable daily-close sheet (popup window), laid out from an already-loaded report. */
export function buildDailyClosePrintHtml(report: CinemaServiceAgent.DailyCloseDTO, labels: DailyClosePrintLabels): string {
  const s = labels.summary;
  const tenderRows = (report.tenders ?? []).map(t =>
    `<tr><td>${escapeHtml(labels.tenderName(t.method))}</td><td class="n">${escapeHtml(t.count)}</td><td class="n">${escapeHtml(labels.money(t.amount))}</td></tr>`).join('');
  const summaryRows: [string, string][] = [
    [s.paymentsTotal, labels.money(report.paymentsTotal)],
    [s.moneyCollected, labels.money(report.moneyCollected)],
    [s.refunds, `${report.refundCount ?? 0} / ${labels.money(report.refundAmount)}`],
    [s.exchanges, String(report.exchangeCount ?? 0)],
    [s.comps, `${report.compCount ?? 0} / ${labels.money(report.compAmount)}`],
    [s.netCollected, labels.money(report.netCollected)],
    [s.tickets, String(report.ticketsSold ?? 0)],
    [s.foodItems, String(report.foodItemsSold ?? 0)],
    [s.foodRevenue, labels.money(report.foodRevenue)],
    [s.totalVariance, labels.money(report.totalVariance)],
  ];
  const summary = summaryRows.map(([k, v]) => `<tr><td>${escapeHtml(k)}</td><td class="n">${escapeHtml(v)}</td></tr>`).join('');

  return `<!doctype html>
<html><head><meta charset="utf-8"><title>${escapeHtml(labels.title)}</title>
<style>
  body { font-family: sans-serif; margin: 16px; font-size: 13px; }
  h1 { font-size: 18px; margin: 0 0 12px; }
  h2 { font-size: 14px; margin: 16px 0 6px; }
  table { border-collapse: collapse; width: 100%; max-width: 520px; }
  td, th { border-bottom: 1px solid #ccc; padding: 4px 8px; text-align: left; }
  .n { text-align: right; }
</style></head><body>
<h1>${escapeHtml(labels.heading)}</h1>
<h2>${escapeHtml(labels.tenders)}</h2>
<table><tr><th>${escapeHtml(labels.tender)}</th><th class="n">${escapeHtml(labels.count)}</th><th class="n">${escapeHtml(labels.amount)}</th></tr>${tenderRows}</table>
<h2>&nbsp;</h2>
<table>${summary}</table>
</body></html>`;
}
