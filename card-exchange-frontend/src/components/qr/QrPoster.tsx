import { useEffect, useState } from 'react';
import QRCode from 'qrcode';
import { useTranslation } from 'react-i18next';
import { Copy, Download, Printer } from 'lucide-react';
import BrandLogo from '../brand/BrandLogo';

interface QrPosterProps {
  url: string;
  /** Riga principale del cartello, es. il nome del negozio */
  title: string;
  /** Invito all'azione sotto il QR */
  cta: string;
  subtitle?: string;
  /** Nome del file PNG scaricato */
  fileName: string;
}

/**
 * Cartello stampabile con QR: logo, titolo, QR grande e link leggibile.
 * In stampa resta visibile solo il cartello (classe print-area, vedi index.css).
 */
export default function QrPoster({ url, title, cta, subtitle, fileName }: QrPosterProps) {
  const { t } = useTranslation('qr');
  const [svg, setSvg] = useState('');
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    // Correzione d'errore alta: il QR resta leggibile anche se stampato piccolo o un po' rovinato
    QRCode.toString(url, { type: 'svg', errorCorrectionLevel: 'H', margin: 1, color: { dark: '#122548', light: '#ffffff' } })
      .then(setSvg)
      .catch(() => setSvg(''));
  }, [url]);

  const downloadPng = async () => {
    const dataUrl = await QRCode.toDataURL(url, { errorCorrectionLevel: 'H', margin: 2, width: 1024, color: { dark: '#122548', light: '#ffffff' } });
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = `${fileName}.png`;
    a.click();
  };

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      /* il link resta visibile e selezionabile */
    }
  };

  return (
    <div className="space-y-4">
      <div className="print-area mx-auto w-full max-w-sm bg-white rounded-3xl border border-border shadow-sm p-6 text-center text-[#0f1f3d]">
        <div className="flex justify-center"><BrandLogo layout="horizontal" theme="light" height={34} /></div>
        <h1 className="mt-5 text-2xl font-extrabold leading-tight text-balance">{title}</h1>
        {subtitle && <p className="mt-1 text-sm text-[#4c5b78]">{subtitle}</p>}
        <div
          className="mx-auto mt-5 w-64 max-w-full aspect-square [&>svg]:w-full [&>svg]:h-full"
          role="img"
          aria-label={t('qrAlt', { url })}
          dangerouslySetInnerHTML={{ __html: svg }}
        />
        <p className="mt-4 text-base font-bold">{cta}</p>
        <p className="mt-1 text-xs text-[#4c5b78] break-all select-all">{url.replace(/^https?:\/\//, '')}</p>
      </div>

      <div className="no-print flex flex-wrap justify-center gap-2">
        <button type="button" onClick={() => window.print()} className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl bg-primary text-white text-sm font-semibold hover:bg-primary-light">
          <Printer size={16} /> {t('print')}
        </button>
        <button type="button" onClick={downloadPng} className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl bg-white border border-border text-sm font-semibold text-text hover:border-primary/40">
          <Download size={16} /> {t('downloadPng')}
        </button>
        <button type="button" onClick={copy} className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl bg-white border border-border text-sm font-semibold text-text hover:border-primary/40">
          <Copy size={16} /> {copied ? t('copied') : t('copyLink')}
        </button>
      </div>
    </div>
  );
}
