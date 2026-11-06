import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, ExternalLink, Loader2, Megaphone } from 'lucide-react';
import { sponsored, type SponsorInfo } from '../api';
import EmptyState from '../components/ui/EmptyState';

/** Pagina interna dello sponsor: il sito si apre passando dal server, che conta solo il totale dei clic. */
export default function SponsorPage() {
  const { t } = useTranslation('promotions');
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [s, setS] = useState<SponsorInfo | null | undefined>(undefined);

  useEffect(() => { sponsored.sponsor(Number(id)).then(({ data }) => setS(data)).catch(() => setS(null)); }, [id]);

  if (s === undefined) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  if (s === null) return <EmptyState icon={Megaphone} title={t('sponsorPage.notFound')} />;
  return (
    <div className="pb-24 space-y-4 max-w-xl">
      <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center"><ArrowLeft size={18} /></button>
      <div className="bg-white rounded-2xl border border-border/60 p-5 text-center space-y-3">
        {s.logoUrl && <img src={s.logoUrl} alt="" className="w-20 h-20 mx-auto object-contain" />}
        <h1 className="text-xl font-bold text-text">{s.name}</h1>
        {s.description && <p className="text-sm text-text-secondary whitespace-pre-line">{s.description}</p>}
        {s.hasWebsite && (
          <a href={`/api/sponsors/${s.id}/go`} target="_blank" rel="noopener noreferrer sponsored"
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl bg-primary text-white text-sm font-semibold">
            <ExternalLink size={15} /> {t('sponsorPage.visit')}
          </a>
        )}
        <p className="text-[11px] text-text-muted">{t('sponsorPage.note')}</p>
      </div>
    </div>
  );
}
