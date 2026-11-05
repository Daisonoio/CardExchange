import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft } from 'lucide-react';
import BrandLogo from '../../components/brand/BrandLogo';
import { LEGAL } from '../../config/legal';
import { useFormat } from '../../i18n/format';

type Doc = 'privacy' | 'terms' | 'cookies';
type Section = { title: string; body: string[] };

/** Informativa privacy, termini d'uso e cookie: pagine pubbliche, leggibili anche senza account. */
export default function LegalPage({ doc }: { doc: Doc }) {
  const { t } = useTranslation('legal');
  const { date } = useFormat();
  const navigate = useNavigate();
  const sections = t(`${doc}.sections`, { returnObjects: true, ...LEGAL }) as Section[];
  const placeholders = Object.values(LEGAL).some((v) => String(v).startsWith('['));

  return (
    <div className="min-h-screen bg-surface-dark">
      <header className="bg-white border-b border-border">
        <div className="max-w-2xl mx-auto px-4 h-14 flex items-center justify-between gap-3">
          <button onClick={() => (window.history.length > 1 ? navigate(-1) : navigate('/'))} className="inline-flex items-center gap-1.5 text-sm font-semibold text-primary">
            <ArrowLeft size={16} /> {t('back')}
          </button>
          <Link to="/" aria-label="CardExchange"><BrandLogo layout="horizontal" theme="light" height={28} title="" /></Link>
        </div>
      </header>
      <main className="max-w-2xl mx-auto px-4 py-8 space-y-6">
        <nav className="flex flex-wrap gap-2" aria-label={t('nav.privacy')}>
          {(['privacy', 'terms', 'cookies'] as Doc[]).map((d) => (
            <NavLink key={d} to={`/${d}`} className={({ isActive }) => `px-3 py-1.5 rounded-full text-sm font-semibold ${isActive ? 'bg-primary text-white' : 'bg-white border border-border text-text-secondary'}`}>
              {t(`nav.${d}`)}
            </NavLink>
          ))}
        </nav>
        <div>
          <h1 className="text-3xl font-extrabold text-text">{t(`${doc}.title`)}</h1>
          <p className="text-sm text-text-muted mt-1">{t('updated', { date: date(LEGAL.updated) })}</p>
          {placeholders && <p className="mt-3 text-sm p-3 rounded-xl bg-amber-50 text-amber-800">{t('draft')}</p>}
        </div>
        <p className="text-text">{t(`${doc}.intro`)}</p>
        {Array.isArray(sections) && sections.map((s) => (
          <section key={s.title} className="space-y-2">
            <h2 className="text-lg font-bold text-text">{s.title}</h2>
            {s.body.map((p, i) => <p key={i} className="text-text-secondary leading-relaxed">{p}</p>)}
          </section>
        ))}
      </main>
    </div>
  );
}
