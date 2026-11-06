import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Camera, CheckCircle2, ImageUp, Loader2, RotateCcw, ScanLine } from 'lucide-react';
import type { GameCard } from '../../types';
import { extractNameCandidates, findPrintCode } from '../../utils/scanText';
import { fingerprintVariants, toBase64 } from '../../utils/cardFingerprint';
import { scan, type ScanCandidate } from '../../api';
import BottomSheet from '../ui/BottomSheet';
import Button from '../ui/Button';

type Phase = 'camera' | 'reading' | 'results' | 'error';

// Proporzioni di una carta collezionabile (63 × 88 mm)
const CARD_RATIO = 63 / 88;

// Il motore OCR (tesseract.js, ~3 MB più i dati della lingua) si carica solo quando serve e resta in memoria tra una scansione e l'altra
type OcrWorker = { recognize: (image: HTMLCanvasElement) => Promise<{ data: { text: string } }>; terminate: () => Promise<unknown> };
let workerPromise: Promise<OcrWorker> | null = null;
function getWorker(): Promise<OcrWorker> {
  workerPromise ??= import('tesseract.js').then(({ createWorker }) => createWorker('eng') as unknown as Promise<OcrWorker>);
  return workerPromise;
}

type Rect = { x: number; y: number; w: number; h: number };

/** Area della carta nell'inquadratura: la guida al centro, con le proporzioni di una carta. */
function guideRect(sw: number, sh: number): Rect {
  let h = sh * 0.86;
  let w = h * CARD_RATIO;
  if (w > sw * 0.95) { w = sw * 0.95; h = w / CARD_RATIO; }
  return { x: (sw - w) / 2, y: (sh - h) / 2, w, h };
}

/** Ritaglia, ingrandisce, porta a scala di grigi e aumenta il contrasto: l'OCR legge meglio. */
function prepareCanvas(source: CanvasImageSource, r: Rect, targetWidth: number): HTMLCanvasElement {
  const scale = targetWidth / r.w;
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(r.w * scale);
  canvas.height = Math.round(r.h * scale);
  const ctx = canvas.getContext('2d', { willReadFrequently: true })!;
  ctx.drawImage(source, r.x, r.y, r.w, r.h, 0, 0, canvas.width, canvas.height);
  const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
  const d = img.data;
  let min = 255, max = 0;
  for (let i = 0; i < d.length; i += 4) {
    const g = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
    d[i] = g;
    if (g < min) min = g;
    if (g > max) max = g;
  }
  const range = Math.max(1, max - min);
  for (let i = 0; i < d.length; i += 4) {
    const v = ((d[i] - min) * 255) / range;
    d[i] = d[i + 1] = d[i + 2] = v;
  }
  ctx.putImageData(img, 0, 0);
  return canvas;
}

interface CardScannerProps {
  open: boolean;
  onClose: () => void;
  /** Ricerca nel catalogo del gioco selezionato (la stessa della ricerca testuale). */
  onSearch: (query: string) => Promise<GameCard[]>;
  onSelect: (card: GameCard) => void;
  /** Chiave del gioco (es. "lorcana"), per adattare i candidati. */
  gameKey?: string;
  /** Gioco selezionato: il riconoscimento per immagine cerca solo tra le sue carte. */
  gameId?: number | null;
}

const toGameCard = (c: ScanCandidate): GameCard => ({
  externalId: c.externalId ?? String(c.cardInfoId), name: c.name, setName: c.setName, rarity: c.rarity,
  imageSmall: c.imageSmall, imageLarge: c.imageLarge, subtitle: c.number,
});

/**
 * Riconoscimento di una carta con la fotocamera. Due strade insieme:
 * l'immagine (impronta calcolata sul telefono, al server arrivano solo numeri) e il testo letto con l'OCR,
 * che conferma la stampa giusta. La foto non lascia mai il dispositivo.
 */
export default function CardScanner({ open, onClose, onSearch, onSelect, gameKey, gameId }: CardScannerProps) {
  const { t } = useTranslation('scanner');
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const [phase, setPhase] = useState<Phase>('camera');
  const [cameraError, setCameraError] = useState<string | null>(null);
  const [candidates, setCandidates] = useState<string[]>([]);
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<GameCard[]>([]);
  const [searching, setSearching] = useState(false);
  const [status, setStatus] = useState('');
  const [visual, setVisual] = useState<{ confident: boolean; candidates: ScanCandidate[] } | null>(null);
  const [readingText, setReadingText] = useState(false);

  const stopCamera = useCallback(() => {
    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;
  }, []);

  const startCamera = useCallback(async () => {
    setCameraError(null);
    setPhase('camera');
    if (!navigator.mediaDevices?.getUserMedia) {
      setCameraError(t('noCamera'));
      return;
    }
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 } },
        audio: false,
      });
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play().catch(() => undefined);
      }
    } catch {
      setCameraError(t('cameraDenied'));
    }
  }, [t]);

  useEffect(() => {
    if (!open) return;
    const timer = setTimeout(startCamera, 0); // dopo l'apertura del pannello, così il video esiste
    return () => { clearTimeout(timer); stopCamera(); };
  }, [open, startCamera, stopCamera]);

  const search = useCallback(async (q: string) => {
    setQuery(q);
    setSearching(true);
    try {
      // Prima le carte il cui nome coincide con quanto letto, poi quelle che lo contengono
      const norm = (s: string) => s.toLowerCase().replace(/[^\p{L}\p{N} ]/gu, '').trim();
      const target = norm(q);
      const rank = (name: string) => {
        const faces = name.split(' // ').map(norm);
        if (norm(name) === target) return 0;
        return faces.includes(target) ? 1 : faces.some((f) => f.startsWith(target)) ? 2 : 3;
      };
      const found = await onSearch(q);
      setResults(found.map((card, i) => ({ card, i })).sort((a, b) => rank(a.card.name) - rank(b.card.name) || a.i - b.i).map((x) => x.card));
    } catch {
      setResults([]);
    } finally {
      setSearching(false);
    }
  }, [onSearch]);

  /** Impronta visiva: risultato in un attimo, prima ancora della lettura del testo. */
  const matchImage = async (vectors: string[], names?: string[], number?: string | null) => {
    try {
      const { data } = await scan.match({ gameId, vectors, names, number });
      if (data.candidates.length > 0) setVisual({ confident: !!data.confident, candidates: data.candidates.slice(0, 4) });
      return data;
    } catch {
      return null;
    }
  };

  /** Legge la carta due volte: la fascia del titolo ingrandita (il nome) e la carta intera (codici, altri testi). */
  const readText = async (source: CanvasImageSource, card: Rect) => {
    const worker = await getWorker();
    // Il nome è in alto per Magic, Pokémon, Yu-Gi-Oh!; Lorcana e One Piece lo hanno più in basso: la lettura intera li copre
    const titleBand = prepareCanvas(source, { x: card.x, y: card.y, w: card.w, h: card.h * 0.13 }, 1600);
    const whole = prepareCanvas(source, card, 1100);
    const [title, full] = [await worker.recognize(titleBand), await worker.recognize(whole)];
    const lines = [...title.data.text.split('\n'), ...full.data.text.split('\n')].map((l) => l.trim()).filter(Boolean);
    const code = findPrintCode(full.data.text);
    let names = extractNameCandidates(lines, 3);
    // Lorcana: nome e versione sono su due righe ("ELSA" / "Snow Queen")
    if (gameKey === 'lorcana' && names.length >= 2) names = [`${names[0]} ${names[1]}`, ...names];
    return { code, names };
  };

  // Ultima foto, per leggere il testo solo se serve (immagine non sicura o "Non è questa?")
  const lastShot = useRef<{ canvas: HTMLCanvasElement; card: Rect; vectors: string[] } | null>(null);

  const readImage = async (source: CanvasImageSource, card: Rect) => {
    setPhase('reading');
    setVisual(null);
    setStatus(t('matching'));
    try {
      const vectors = fingerprintVariants(source, card).map(toBase64);
      // Copia della foto: il bitmap di una foto caricata viene chiuso subito dopo
      const copy = document.createElement('canvas');
      copy.width = Math.round(card.w); copy.height = Math.round(card.h);
      copy.getContext('2d')!.drawImage(source, card.x, card.y, card.w, card.h, 0, 0, copy.width, copy.height);
      lastShot.current = { canvas: copy, card: { x: 0, y: 0, w: copy.width, h: copy.height }, vectors };
      const first = await matchImage(vectors);
      if (first?.confident) { setPhase('results'); return; }
      await refineWithText(!!first?.candidates.length);
    } catch {
      setReadingText(false);
      setPhase('error');
    }
  };

  /** Lettura del testo (OCR): conferma la stampa e permette la ricerca per nome. */
  const refineWithText = async (recognized: boolean) => {
    const shot = lastShot.current;
    if (!shot) return;
    const { canvas: source, card, vectors } = shot;
    try {
      // Con l'immagine riconosciuta si mostrano subito i risultati; il testo arriva dopo e conferma la stampa
      if (recognized) setPhase('results');
      else setStatus(t('loadingEngine'));
      setReadingText(true);
      const text = await readText(source, card).catch(() => ({ code: null as string | null, names: [] as string[] }));
      setReadingText(false);
      const list = [...(text.code ? [text.code] : []), ...text.names].slice(0, 4);
      setCandidates(list);
      setPhase('results');
      if (recognized && (text.names.length > 0 || text.code)) await matchImage(vectors, text.names, text.code);
      if (list.length > 0) await search(list[0]);
      else setResults([]);
    } catch {
      setReadingText(false);
      if (!recognized) setPhase('error');
    }
  };

  const capture = async () => {
    const video = videoRef.current;
    if (!video || !video.videoWidth) return;
    const frame = document.createElement('canvas');
    frame.width = video.videoWidth;
    frame.height = video.videoHeight;
    frame.getContext('2d')!.drawImage(video, 0, 0);
    stopCamera();
    await readImage(frame, guideRect(frame.width, frame.height));
  };

  const fromFile = async (file: File | undefined) => {
    if (!file) return;
    stopCamera();
    const bitmap = await createImageBitmap(file).catch(() => null);
    if (!bitmap) { setPhase('error'); return; }
    // Una foto caricata di solito contiene solo la carta: si usa tutta l'immagine
    await readImage(bitmap, { x: 0, y: 0, w: bitmap.width, h: bitmap.height });
    bitmap.close();
  };

  const close = () => {
    stopCamera();
    setPhase('camera'); setCandidates([]); setResults([]); setQuery(''); setVisual(null);
    onClose();
  };

  const retry = () => {
    setCandidates([]); setResults([]); setQuery(''); setVisual(null);
    startCamera();
  };

  return (
    <BottomSheet open={open} onClose={close} title={t('title')}>
      <input ref={fileRef} type="file" accept="image/*" capture="environment" className="sr-only"
        onChange={(e) => { fromFile(e.target.files?.[0]); e.target.value = ''; }} />

      {phase === 'camera' && (
        <div className="space-y-3">
          {cameraError ? (
            <div className="p-4 rounded-2xl bg-surface-dark text-sm text-text-secondary text-center space-y-3">
              <p>{cameraError}</p>
              <Button variant="outline" onClick={() => fileRef.current?.click()}><ImageUp size={16} /> {t('uploadPhoto')}</Button>
            </div>
          ) : (
            <>
              <div className="relative rounded-2xl overflow-hidden bg-black aspect-[3/4]">
                <video ref={videoRef} playsInline muted className="absolute inset-0 w-full h-full object-cover" />
                {/* Guida: la carta va inquadrata qui, con il nome ben visibile */}
                <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                  <div className="h-[86%] aspect-[63/88] rounded-xl border-2 border-white/90 shadow-[0_0_0_9999px_rgba(0,0,0,0.45)]" />
                </div>
                <p className="absolute bottom-2 inset-x-0 text-center text-xs text-white drop-shadow">{t('hint')}</p>
              </div>
              <div className="flex gap-2">
                <Button className="flex-1" onClick={capture}><Camera size={16} /> {t('capture')}</Button>
                <Button variant="outline" onClick={() => fileRef.current?.click()} aria-label={t('uploadPhoto')}><ImageUp size={16} /></Button>
              </div>
              <p className="text-[11px] text-text-muted">{t('privacy')}</p>
            </>
          )}
        </div>
      )}

      {phase === 'reading' && (
        <div className="flex flex-col items-center gap-3 py-12 text-sm text-text-secondary">
          <ScanLine size={32} className="text-primary animate-pulse" />
          <span className="flex items-center gap-2"><Loader2 size={14} className="animate-spin" /> {status}</span>
        </div>
      )}

      {phase === 'error' && (
        <div className="py-8 text-center space-y-3">
          <p className="text-sm text-danger">{t('failed')}</p>
          <Button variant="outline" onClick={retry}><RotateCcw size={16} /> {t('retry')}</Button>
        </div>
      )}

      {phase === 'results' && (
        <div className="space-y-3">
          {visual && (
            <div data-testid="scan-visual">
              <p className="text-xs text-text-muted mb-1.5 flex items-center gap-1.5">
                {visual.confident ? <><CheckCircle2 size={13} className="text-green-600" /> {t('visualConfident')}</> : t('visualMaybe')}
              </p>
              <ul className="space-y-1.5">
                {visual.candidates.slice(0, visual.confident ? 1 : 4).map((c, i) => (
                  <li key={c.cardInfoId}>
                    <button onClick={() => { onSelect(toGameCard(c)); close(); }}
                      className={`w-full flex items-center gap-3 p-2 rounded-xl text-left border ${i === 0 && visual.confident ? 'border-green-500 bg-green-50' : 'border-border bg-white hover:bg-surface-dark'}`}>
                      {c.imageSmall ? <img src={c.imageSmall} alt="" className={`${i === 0 && visual.confident ? 'w-14 h-20' : 'w-10 h-14'} rounded object-cover bg-surface-dark`} /> : <span className="w-10 h-14 rounded bg-surface-dark" />}
                      <span className="flex-1 min-w-0">
                        <span className="block text-sm font-medium text-text truncate">{c.name}</span>
                        <span className="block text-[11px] text-text-muted truncate">{[c.setName, c.number, c.rarity].filter(Boolean).join(' · ')}</span>
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
              {visual.confident && visual.candidates.length > 1 && (
                <button type="button" onClick={() => { setVisual({ ...visual, confident: false }); refineWithText(true); }} className="mt-1 text-xs text-primary font-semibold">{t('notThisOne')}</button>
              )}
            </div>
          )}
          {readingText && <p className="text-xs text-text-muted flex items-center gap-1.5"><Loader2 size={12} className="animate-spin" /> {t('readingText')}</p>}
          {!visual?.confident && <>
          {candidates.length > 0 ? (
            <div>
              <p className="text-xs text-text-muted mb-1.5">{t('recognized')}</p>
              <div className="flex flex-wrap gap-1.5">
                {candidates.map((c) => (
                  <button key={c} onClick={() => search(c)}
                    className={`px-2.5 py-1 rounded-full text-xs border ${query === c ? 'bg-primary text-white border-primary' : 'bg-white border-border text-text'}`}>
                    {c}
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <p className="text-sm text-text-secondary">{t('nothingRead')}</p>
          )}

          <form onSubmit={(e) => { e.preventDefault(); if (query.trim().length >= 2) search(query.trim()); }} className="flex gap-2">
            <input value={query} onChange={(e) => setQuery(e.target.value)} aria-label={t('editQuery')}
              className="flex-1 px-3 py-2 rounded-xl border border-border text-sm" placeholder={t('editQuery')} />
            <Button type="submit" variant="outline" size="sm">{t('search')}</Button>
          </form>

          {searching ? (
            <div className="flex justify-center py-6"><Loader2 className="animate-spin text-primary" /></div>
          ) : results.length === 0 ? (
            query && <p className="text-sm text-text-muted text-center py-4">{t('noResults')}</p>
          ) : (
            <ul className="max-h-[45vh] overflow-y-auto divide-y divide-border/60 rounded-xl border border-border">
              {results.map((card) => (
                <li key={card.externalId}>
                  <button onClick={() => { onSelect(card); close(); }} className="w-full flex items-center gap-3 p-2 text-left hover:bg-surface-dark">
                    {card.imageSmall ? <img src={card.imageSmall} alt="" className="w-10 h-14 rounded object-cover bg-surface-dark" /> : <span className="w-10 h-14 rounded bg-surface-dark" />}
                    <span className="flex-1 min-w-0">
                      <span className="block text-sm font-medium text-text truncate">{card.name}</span>
                      <span className="block text-[11px] text-text-muted truncate">{[card.setName, card.rarity].filter(Boolean).join(' · ')}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}

          </>}
          <Button variant="ghost" className="w-full" onClick={retry}><RotateCcw size={16} /> {t('scanAnother')}</Button>
        </div>
      )}
    </BottomSheet>
  );
}
