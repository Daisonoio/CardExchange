import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

/** Collegamenti a privacy, termini e cookie, in fondo alle pagine di accesso. */
export default function LegalLinks({ light = false }: { light?: boolean }) {
  const { t } = useTranslation('legal');
  const cls = light ? 'text-white/70 hover:text-white' : 'text-text-muted hover:text-text';
  return (
    <nav aria-label={t('nav.privacy')} className="flex justify-center gap-4 text-xs mt-6">
      <Link to="/privacy" className={cls}>{t('nav.privacy')}</Link>
      <Link to="/terms" className={cls}>{t('nav.terms')}</Link>
      <Link to="/cookies" className={cls}>{t('nav.cookies')}</Link>
    </nav>
  );
}
