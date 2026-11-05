import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft } from 'lucide-react';
import BrandLogo from '../components/brand/BrandLogo';
import NotificationsGuide from '../components/notifications/NotificationsGuide';

/** Guida pubblica alle notifiche (anche senza account): i negozi possono condividerne il link. */
export default function NotificationsGuidePage() {
  const { t } = useTranslation('notifications');
  const navigate = useNavigate();
  return (
    <div className="min-h-screen bg-surface-dark">
      <header className="bg-white border-b border-border">
        <div className="max-w-2xl mx-auto px-4 h-14 flex items-center justify-between gap-3">
          <button onClick={() => (window.history.length > 1 ? navigate(-1) : navigate('/'))} className="inline-flex items-center gap-1.5 text-sm font-semibold text-primary">
            <ArrowLeft size={16} /> {t('guide.back')}
          </button>
          <Link to="/" aria-label="CardExchange"><BrandLogo layout="horizontal" theme="light" height={28} title="" /></Link>
        </div>
      </header>
      <main className="max-w-2xl mx-auto px-4 py-6 space-y-4">
        <h1 className="text-2xl font-extrabold text-text">{t('guide.title')}</h1>
        <NotificationsGuide showAll />
        <Link to="/home" className="inline-block px-4 py-2 rounded-xl bg-primary text-white text-sm font-semibold">{t('guide.openApp')}</Link>
      </main>
    </div>
  );
}
