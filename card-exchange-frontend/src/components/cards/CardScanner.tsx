import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Camera, ImageUp, Loader2, RotateCcw, ScanLine } from 'lucide-react';
import type { GameCard } from '../../types';
import { extractNameCandidates, findPrintCode } from '../../utils/scanText';
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

/** Ritaglia l'area della guida, porta a scala di grigi e aumenta il contrasto: l'OCR legge meglio. */
function prepareCanvas(source: CanvasImageSource, sw: number, sh: number): HTMLCanvasElement {
  // Area della carta al centro dell'immagine, con le proporzioni della guida
  let cw = sw * 0.8;
  let ch = cw / CARD_RATIO;
  if (ch > sh * 0.9) { ch = sh * 0.9; cw = ch * CARD_RATIO; }
  const sx = (sw - cw) / 2;
  const sy = (sh - ch) / 2;
  const scale = Math.min(1, 1200 / cw) * Math.max(1, 900 / cw);
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(cw * scale);
  canvas.height = Math.round(ch * scale);
  const ctx = canvas.getContext('2d', { willReadFrequently: true })!;
  ctx.drawImage(source, sx, sy, cw, ch, 0, 0, canvas.width, canvas.height);
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
}

/**
 * Riconoscimento di una carta con la fotocamera, tutto nel browser: la foto non lascia il dispositivo,
 * al server arriva solo il nome letto. Si sceglie poi la carta giusta tra i risultati della ricerca.
 */
export default function CardScanner({ open, onClose, onSearch, onSelect, gameKey }: CardScannerProps) {
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
      setResults(await onSearch(q));
    } catch {
      setResults([]);
    } finally {
      setSearching(false);
    }
  }, [onSearch]);

  const readImage = async (canvas: HTMLCanvasElement) => {
    setPhase('reading');
    setStatus(t('loadingEngine'));
    try {
      const worker = await getWorker();
      setStatus(t('reading'));
      const { data } = await worker.recognize(canvas);
      const lines = data.text.split('\n').map((l) => l.trim()).filter(Boolean);
      const code = findPrintCode(data.text);
      let names = extractNameCandidates(lines, 3);
      // Lorcana: nome e versione sono su due righe ("ELSA" / "Snow Queen")
      if (gameKey === 'lorcana' && names.length >= 2) names = [`${names[0]} ${names[1]}`, ...names];
      const list = [...(code ? [code] : []), ...names].slice(0, 4);
      setCandidates(list);
      setPhase('results');
      if (list.length > 0) await search(list[0]);
      else setResults([]);
    } catch {
      setPhase('error');
    }
  };

  const capture = async () => {
    const video = videoRef.current;
    if (!video || !video.videoWidth) return;
    const canvas = prepareCanvas(video, video.videoWidth, video.videoHeight);
    stopCamera();
    await readImage(canvas);
  };

  const fromFile = async (file: File | undefined) => {
    if (!file) return;
    stopCamera();
    const bitmap = await createImageBitmap(file).catch(() => null);
    if (!bitmap) { setPhase('error'); return; }
    // Una foto caricata di solito contiene solo la carta: si usa quasi tutta l'immagine
    const canvas = prepareCanvas(bitmap, bitmap.width, bitmap.height);
    bitmap.close();
    await readImage(canvas);
  };

  const close = () => {
    stopCamera();
    setPhase('camera'); setCandidates([]); setResults([]); setQuery('');
    onClose();
  };

  const retry = () => {
    setCandidates([]); setResults([]); setQuery('');
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

          <Button variant="ghost" className="w-full" onClick={retry}><RotateCcw size={16} /> {t('scanAnother')}</Button>
        </div>
      )}
    </BottomSheet>
  );
}
