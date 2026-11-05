import { useTranslation } from 'react-i18next';
import { WifiOff } from 'lucide-react';
import BrandLogo from '../brand/BrandLogo';

/** Il server non risponde: la sessione resta valida e si può riprovare (nessun rimando al login). */
export default function ConnectionErrorScreen({ onRetry }: { onRetry: () => void }) {
  const { t } = useTranslation('layout');
  return (
    <div className="min-h-screen flex flex-col items-center justify-center gap-6 px-6 text-center bg-surface-dark">
      <BrandLogo layout="horizontal" theme="light" height={32} />
      <div className="w-14 h-14 rounded-full bg-white border border-border flex items-center justify-center text-text-secondary">
        <WifiOff size={24} />
      </div>
      <div className="space-y-1 max-w-xs">
        <h1 className="text-lg font-bold text-text">{t('connection.title')}</h1>
        <p className="text-sm text-text-secondary">{t('connection.description')}</p>
      </div>
      <button type="button" onClick={onRetry} className="px-6 py-3 rounded-xl bg-primary text-white font-semibold hover:bg-primary-light">
        {t('connection.retry')}
      </button>
    </div>
  );
}
