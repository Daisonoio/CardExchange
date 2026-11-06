import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Loader2, QrCode } from 'lucide-react';
import { broadcasts, showcase, stores } from '../api';
import { useAuth } from '../context/AuthContext';
import QrPoster from '../components/qr/QrPoster';
import EmptyState from '../components/ui/EmptyState';

type Poster = { url: string; title: string; subtitle?: string; cta: string; fileName: string };

/**
 * QR stampabili: /qr/store/:id (negozio, solo proprietario), /qr/me (profilo), /qr/live/:id ("Sono qui").
 */
export default function QrPage() {
  const { t } = useTranslation('qr');
  const { kind, id } = useParams<{ kind: string; id?: string }>();
  const { user } = useAuth();
  const navigate = useNavigate();
  const [poster, setPoster] = useState<Poster | null | undefined>(undefined);
  const origin = window.location.origin;

  useEffect(() => {
    if (!user) return;
    if (kind === 'me') {
      setPoster({
        url: `${origin}/collection/${user.id}`,
        title: `@${user.username}`,
        subtitle: t('me.subtitle'),
        cta: t('me.cta'),
        fileName: `cardexchange-${user.username}`,
      });
      return;
    }
    if (kind === 'store' && id) {
      stores.get(Number(id)).then(({ data }) => {
        if (!data.isOwner || !data.slug || data.status !== 'Verified') { setPoster(null); return; }
        setPoster({
          url: `${origin}/s/${data.slug}`,
          title: data.name,
          subtitle: t('store.subtitle'),
          cta: t('store.cta'),
          fileName: `cardexchange-${data.slug}`,
        });
      }).catch(() => setPoster(null));
      return;
    }
    if (kind === 'showcase') {
      showcase.mine().then(({ data }) => {
        if (!data.exists || !data.hasPlan) { setPoster(null); return; }
        setPoster({
          url: `${origin}/v/${data.slug}`,
          title: data.displayName,
          subtitle: t('showcase.subtitle'),
          cta: t('showcase.cta'),
          fileName: `cardexchange-banco-${data.slug}`,
        });
      }).catch(() => setPoster(null));
      return;
    }
    if (kind === 'live' && id) {
      broadcasts.get(Number(id)).then(({ data }) => {
        if (!data.isMine) { setPoster(null); return; }
        setPoster({
          url: `${origin}/live/${data.id}`,
          title: data.store ? data.store.name : `@${data.author.username}`,
          subtitle: [data.placeName, data.spotDetail].filter(Boolean).join(' · '),
          cta: t('live.cta'),
          fileName: `cardexchange-sono-qui-${data.id}`,
        });
      }).catch(() => setPoster(null));
      return;
    }
    setPoster(null);
  }, [kind, id, user, origin, t]);

  return (
    <div className="pb-24 space-y-4 max-w-xl mx-auto">
      <div className="no-print flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0">
          <ArrowLeft size={18} />
        </button>
        <div className="min-w-0">
          <h1 className="text-xl font-bold text-text flex items-center gap-2"><QrCode size={20} className="text-primary" /> {t('title')}</h1>
          <p className="text-sm text-text-secondary">{t(`${kind === 'store' ? 'store' : kind === 'live' ? 'live' : kind === 'showcase' ? 'showcase' : 'me'}.hint`)}</p>
        </div>
      </div>
      {poster === undefined && <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>}
      {poster === null && <EmptyState icon={QrCode} title={t('unavailable')} description={t('unavailableDescription')} />}
      {poster && <QrPoster {...poster} />}
    </div>
  );
}
