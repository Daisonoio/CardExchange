/**
 * Lettura minima di un CSV per la lista acquisti: solo per trovare le colonne dei prezzi.
 * Le carte (nome, set, numero, quantità, condizione) le riconosce l'anteprima di import del server.
 */
export interface CsvPrices { cash?: number; credit?: number }

const norm = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]/g, '');

const CASH = ['prezzocontanti', 'contanti', 'cash', 'pricecash', 'cashprice', 'buyprice', 'prezzo', 'price', 'prezzoacquisto'];
const CREDIT = ['prezzocredito', 'credito', 'credit', 'pricecredit', 'creditprice', 'storecredit', 'creditonegozio'];

function splitLine(line: string, sep: string): string[] {
  const out: string[] = [];
  let cur = '';
  let quoted = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (quoted) {
      if (ch === '"' && line[i + 1] === '"') { cur += '"'; i++; } else if (ch === '"') quoted = false; else cur += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === sep) { out.push(cur); cur = ''; } else cur += ch;
  }
  out.push(cur);
  return out.map((v) => v.trim());
}

function parseNumber(v: string | undefined): number | undefined {
  if (!v) return undefined;
  const n = Number(v.replace(/[€\s]/g, '').replace(',', '.'));
  return Number.isFinite(n) && n >= 0 ? n : undefined;
}

/** Prezzi per numero di riga (la prima riga è l'intestazione = 1, come nell'anteprima del server). */
export function readPrices(text: string): Map<number, CsvPrices> {
  const lines = text.replace(/^\uFEFF/, '').split(/\r?\n/);
  const result = new Map<number, CsvPrices>();
  if (lines.length === 0) return result;
  const header = lines[0];
  const sep = [';', '\t', ','].sort((a, b) => header.split(b).length - header.split(a).length)[0];
  const cols = splitLine(header, sep).map(norm);
  const cashIdx = cols.findIndex((c) => CASH.includes(c));
  const creditIdx = cols.findIndex((c) => CREDIT.includes(c));
  for (let i = 1; i < lines.length; i++) {
    if (!lines[i].trim()) continue;
    const v = splitLine(lines[i], sep);
    result.set(i + 1, { cash: cashIdx >= 0 ? parseNumber(v[cashIdx]) : undefined, credit: creditIdx >= 0 ? parseNumber(v[creditIdx]) : undefined });
  }
  return result;
}
