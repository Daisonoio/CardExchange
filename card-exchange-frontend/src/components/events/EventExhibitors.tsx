import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Heart, Radio, Store } from 'lucide-react';
import { eventPackages, type PublicExhibitor } from '../../api';
import { useGame } from '../../context/GameContext';

/** Scheda "Espositori" della fiera: prima chi è qui adesso, poi chi ha le carte che cerchi. */
export default function EventExhibitors({ eventId }: { eventId: number }) {
  const { t } = useTranslation('organizers');
  const { selectedGameId, selectedGame } = useGame();
  const [data, setData] = useState<{ floorPlanImageUrl?: string; exhibitors: PublicExhibitor[] } | null>(null);

  useEffect(() => {
    eventPackages.list(eventId, selectedGameId)
      .then(({ data }) => setData(data.active ? { floorPlanImageUrl: data.floorPlanImageUrl, exhibitors: data.exhibitors ?? [] } : null))
      .catch(() => setData(null));
  }, [eventId, selectedGameId]);

  if (!data) return null;
  return (
    <section className="bg-white rounded-2xl border border-border/60 p-4 space-y-3" data-testid="event-exhibitors">
      <div className="flex items-baseline justify-between">
        <h2 className="text-base font-bold text-text">{t('public.title')}</h2>
        <span className="text-xs text-text-muted">{selectedGame ? t('public.onlyGame', { game: selectedGame.name }) : t('public.count', { count: data.exhibitors.length })}</span>
      </div>
      {data.floorPlanImageUrl && (
        <a href={data.floorPlanImageUrl} target="_blank" rel="noopener noreferrer" className="block">
          <img src={data.floorPlanImageUrl} alt={t('public.floorPlan')} className="w-full max-h-64 object-contain rounded-xl bg-surface-dark" />
        </a>
      )}
      {data.exhibitors.length === 0 ? (
        <p className="text-sm text-text-muted">{t('public.empty')}</p>
      ) : (
        <ul className="divide-y divide-border/60">
          {data.exhibitors.map((x) => (
            <li key={x.id} className="py-2 flex items-center gap-3">
              <span className="w-12 shrink-0 text-center text-[11px] font-bold text-primary bg-primary/10 rounded-lg py-1.5">{x.boothCode}</span>
              <span className="flex-1 min-w-0">
                <Link to={x.storeId ? `/stores/${x.storeId}` : `/collection/${x.userId}`} className="block text-sm font-semibold text-text truncate hover:underline">
                  {x.storeId && <Store size={12} className="inline -mt-0.5 mr-1" />}{x.name}
                </Link>
                {x.description && <span className="block text-xs text-text-secondary truncate">{x.description}</span>}
                <span className="flex flex-wrap gap-2 text-[11px] mt-0.5">
                  {x.liveBroadcastId && <Link to={`/live/${x.liveBroadcastId}`} className="inline-flex items-center gap-0.5 font-semibold text-green-700"><Radio size={11} /> {t('public.live')}</Link>}
                  {x.wantedCards > 0 && <span className="inline-flex items-center gap-0.5 text-rose-600"><Heart size={11} /> {t('public.wanted', { count: x.wantedCards })}</span>}
                </span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
