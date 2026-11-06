import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Loader2, MapPin, Pencil, Radio, Star, Store } from 'lucide-react';
import { showcase as showcaseApi, type Showcase } from '../api';
import FollowButton from '../components/social/FollowButton';
import EmptyState from '../components/ui/EmptyState';
import ShowcaseCardTile from '../components/showcase/ShowcaseCardTile';

/** Vetrina pubblica del Banco: copertina, carte in evidenza, "Sono qui" in corso, Segui. */
export default function ShowcasePage() {
  const { t } = useTranslation('showcase');
  const { slug } = useParams<{ slug: string }>();
  const [data, setData] = useState<Showcase | null | undefined>(undefined);

  useEffect(() => {
    if (!slug) return;
    showcaseApi.get(slug).then(({ data }) => setData(data)).catch(() => setData(null));
  }, [slug]);

  if (data === undefined) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  if (data === null) return <EmptyState icon={Store} title={t('notFound')} />;

  return (
    <div className="pb-24 space-y-4 max-w-3xl">
      <div className="relative rounded-3xl overflow-hidden bg-gradient-to-br from-primary to-[#2c4f86] text-white">
        {data.coverImageUrl && <img src={data.coverImageUrl} alt="" className="absolute inset-0 w-full h-full object-cover opacity-40" />}
        <div className="relative p-5 space-y-2">
          <span className="inline-block text-[10px] font-bold uppercase tracking-wide bg-white/20 rounded-full px-2 py-0.5">{t('badge')}</span>
          <h1 className="text-2xl font-extrabold">{data.displayName}</h1>
          <p className="text-xs text-white/85 flex items-center gap-2">
            @{data.seller.username}
            <span className="inline-flex items-center gap-0.5"><Star size={11} /> {data.seller.reputationScore.toFixed(1)}</span>
            · {t('trades', { count: data.seller.totalTradesCompleted })}
          </p>
          {data.bio && <p className="text-sm text-white/90 whitespace-pre-line">{data.bio}</p>}
          <div className="flex flex-wrap gap-2 pt-1">
            {data.isOwner
              ? <Link to="/showcase" className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-white text-primary text-sm font-semibold"><Pencil size={14} /> {t('edit')}</Link>
              : <FollowButton target={{ userId: data.seller.id }} showCount />}
          </div>
        </div>
      </div>

      {data.live && (
        <Link to={`/live/${data.live.id}`} className="flex items-center gap-3 p-3 rounded-2xl bg-green-50 border border-green-200">
          <Radio size={18} className="text-green-700 shrink-0" />
          <span className="min-w-0">
            <strong className="block text-sm text-text">{t('liveNow', { place: data.live.placeName })}</strong>
            <span className="block text-xs text-text-secondary">
              {[data.live.spotDetail, t('until', { time: new Date(data.live.endsAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) })].filter(Boolean).join(' · ')}
              {data.live.approxKm != null && <> · <MapPin size={11} className="inline -mt-0.5" /> ~{data.live.approxKm} km</>}
            </span>
          </span>
        </Link>
      )}

      {data.featured.length > 0 && (
        <section>
          <h2 className="text-base font-bold text-text mb-2">{t('featured')}</h2>
          <ul className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
            {data.featured.map((c) => <li key={c.id}><ShowcaseCardTile card={c} sellerId={data.seller.id} /></li>)}
          </ul>
        </section>
      )}

      <section>
        <div className="flex items-baseline justify-between mb-2">
          <h2 className="text-base font-bold text-text">{t('allCards')}</h2>
          <span className="text-xs text-text-muted">{t('cardsCount', { count: data.totalCards })}</span>
        </div>
        {data.cards.length === 0 && data.featured.length === 0 ? (
          <p className="text-sm text-text-muted">{t('noCards')}</p>
        ) : (
          <ul className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3" data-testid="showcase-cards">
            {data.cards.map((c) => <li key={c.id}><ShowcaseCardTile card={c} sellerId={data.seller.id} /></li>)}
          </ul>
        )}
        {data.totalCards > data.featured.length + data.cards.length && (
          <Link to={`/collection/${data.seller.id}`} className="block mt-3 text-center text-sm font-semibold text-primary">{t('seeAll')}</Link>
        )}
      </section>
      <p className="text-[11px] text-text-muted">{t('inPerson')}</p>
    </div>
  );
}
