/**
 * Dal testo riconosciuto (OCR) su una foto di carta ricava i nomi più probabili da cercare nel catalogo.
 * Il nome è quasi sempre una delle prime righe; si scartano costi, PS, tipi e numeri di collezione.
 */

// Parole che compaiono in alto sulle carte ma non sono il nome
const NOISE = new Set([
  'basic', 'base', 'stage', 'stadio', 'hp', 'ps', 'pv', 'kp', 'trainer', 'allenatore', 'energy', 'energia', 'item', 'supporter',
  'creature', 'creatura', 'instant', 'istantaneo', 'sorcery', 'stregoneria', 'artifact', 'artefatto', 'enchantment', 'incantesimo',
  'land', 'terra', 'legendary', 'leggendario', 'character', 'action', 'song', 'leader', 'event', 'stage', 'don', 'counter',
  'spell', 'trap', 'card', 'carta', 'magia', 'trappola', 'effect', 'monster', 'mostro', 'illus', 'illustrated', 'wizards',
  'pokemon', 'nintendo', 'konami', 'bandai', 'disney', 'ravensburger', 'tm', 'llc',
]);

/** Codice di stampa Yu-Gi-Oh! (LOB-EN001) o One Piece (OP01-001) se presente: identifica la carta con esattezza. */
export function findPrintCode(text: string): string | null {
  const m = /\b([A-Z]{2,4}\d{0,2})-([A-Z]{0,2}\d{3})\b/.exec(text.toUpperCase().replace(/[–—]/g, '-'));
  return m ? `${m[1]}-${m[2]}` : null;
}

function clean(line: string): string {
  return line
    .replace(/\{[^}]*\}/g, ' ') // simboli di mana {R}{2}
    .replace(/[|_~=<>«»"“”`^*#@{}[\]\\]/g, ' ')
    .replace(/\b(HP|PS|PV)\s*\d+\b/gi, ' ')
    .replace(/\b\d+\s*\/\s*\d+\b/g, ' ') // numero di collezione 123/280
    .replace(/[0-9]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function isNameLike(line: string): boolean {
  const letters = line.replace(/[^\p{L}]/gu, '');
  if (letters.length < 3 || line.length > 40) return false;
  // Troppi simboli rispetto alle lettere: rumore dell'OCR
  if (letters.length / line.replace(/\s/g, '').length < 0.7) return false;
  const words = line.toLowerCase().split(/[\s,-]+/).filter(Boolean);
  const meaningful = words.filter((w) => w.replace(/[^\p{L}]/gu, '').length >= 2 && !NOISE.has(w.replace(/[^\p{L}]/gu, '')));
  return meaningful.length > 0 && meaningful.length >= Math.ceil(words.length / 2);
}

/** Al massimo `limit` candidati, nell'ordine in cui compaiono sulla carta, senza duplicati. */
export function extractNameCandidates(rawLines: string[], limit = 3): string[] {
  const out: string[] = [];
  for (const raw of rawLines) {
    if (findPrintCode(raw)) continue; // codici di stampa, non nomi
    const line = clean(raw);
    if (!isNameLike(line)) continue;
    // Toglie le parole di rumore in coda/testa ("Pikachu HP" → "Pikachu", "BASIC Pikachu" → "Pikachu")
    const words = line.split(' ').filter((w) => !NOISE.has(w.toLowerCase().replace(/[^\p{L}]/gu, '')));
    // Lettere isolate in coda sono simboli letti male (mana, rarità)
    while (words.length > 1 && words[words.length - 1].length === 1) words.pop();
    const candidate = words.join(' ').replace(/^[^\p{L}]+|[^\p{L}!?)']+$/gu, '').trim();
    if (candidate.length < 3) continue;
    if (out.some((o) => o.toLowerCase() === candidate.toLowerCase())) continue;
    out.push(candidate);
    if (out.length >= limit) break;
  }
  return out;
}
