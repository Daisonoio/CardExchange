import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

type Detector = { detect: (source: CanvasImageSource) => Promise<{ rawValue: string }[]> };
type DetectorCtor = new (opts: { formats: string[] }) => Detector;

/**
 * Lettore di QR dalla fotocamera. Usa il lettore del browser quando c'è (Chrome, Android),
 * altrimenti jsQR, caricato solo qui. Chiama onRead una volta per codice letto.
 */
export default function QrReader({ onRead, paused }: { onRead: (value: string) => void; paused?: boolean }) {
  const { t } = useTranslation('loyalty');
  const videoRef = useRef<HTMLVideoElement>(null);
  const [error, setError] = useState<string | null>(null);
  const onReadRef = useRef(onRead);
  const pausedRef = useRef(paused);
  useEffect(() => { onReadRef.current = onRead; pausedRef.current = paused; });

  useEffect(() => {
    let stream: MediaStream | null = null;
    let timer: ReturnType<typeof setTimeout> | null = null;
    let stopped = false;
    let last = '';
    const canvas = document.createElement('canvas');

    const start = async () => {
      if (!navigator.mediaDevices?.getUserMedia) { setError(t('scan.noCamera')); return; }
      try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } }, audio: false });
      } catch {
        setError(t('scan.cameraDenied'));
        return;
      }
      if (stopped) { stream.getTracks().forEach((tr) => tr.stop()); return; }
      const video = videoRef.current!;
      video.srcObject = stream;
      await video.play().catch(() => undefined);

      const Native = (window as unknown as { BarcodeDetector?: DetectorCtor }).BarcodeDetector;
      const native = Native ? new Native({ formats: ['qr_code'] }) : null;
      const jsQR = native ? null : (await import('jsqr')).default;

      const scan = async () => {
        if (stopped) return;
        if (!pausedRef.current && video.videoWidth) {
          let value: string | null = null;
          try {
            if (native) {
              value = (await native.detect(video))[0]?.rawValue ?? null;
            } else if (jsQR) {
              const w = Math.min(640, video.videoWidth);
              const h = Math.round((video.videoHeight * w) / video.videoWidth);
              canvas.width = w; canvas.height = h;
              const ctx = canvas.getContext('2d', { willReadFrequently: true })!;
              ctx.drawImage(video, 0, 0, w, h);
              value = jsQR(ctx.getImageData(0, 0, w, h).data, w, h, { inversionAttempts: 'dontInvert' })?.data ?? null;
            }
          } catch { /* fotogramma non leggibile: si riprova */ }
          if (value && value !== last) {
            last = value;
            onReadRef.current(value);
          }
        }
        timer = setTimeout(scan, 250);
      };
      scan();
    };
    start();
    return () => {
      stopped = true;
      if (timer) clearTimeout(timer);
      stream?.getTracks().forEach((tr) => tr.stop());
    };
  }, [t]);

  if (error) return <p className="p-4 rounded-2xl bg-surface-dark text-sm text-text-secondary text-center">{error}</p>;
  return (
    <div className="relative rounded-2xl overflow-hidden bg-black aspect-square max-w-xs mx-auto">
      <video ref={videoRef} playsInline muted className="absolute inset-0 w-full h-full object-cover" />
      <div className="absolute inset-[18%] rounded-xl border-2 border-white/90 shadow-[0_0_0_9999px_rgba(0,0,0,0.35)] pointer-events-none" />
    </div>
  );
}
