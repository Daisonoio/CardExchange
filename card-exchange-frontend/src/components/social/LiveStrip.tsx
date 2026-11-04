import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { MapPin, Radio, Plus } from 'lucide-react';
import { broadcasts, type LiveBroadcast } from '../../api';
import { useFormat } from '../../i18n/format';

/**
 * "Live ora": dove si trovano adesso i profili che segui (mercatini, fiere, stand).
 * Se non c'è nulla in corso mostra solo l'invito a pubblicare il proprio avviso.
 */
export default function LiveStrip({ coords }: { coords?: { latitude: number; longitude: number } | null }) {
  const { t } = useTranslation('live');
  const { time } = useFormat();
  const [items, setItems] = useState<LiveBroadcast[]>([]);

  const lat = coords?.latitude;
  const lon = coords?.longitude;
  useEffect(() => {
    const load = () => broadcasts.live(lat != null && lon != null ? { latitude: lat, longitude: lon } : undefined)
      .then(({ data }) => setItems(data))
      .catch(() => setItems([]));
    load();
    // Gli avvisi scadono: si aggiorna ogni 2 minuti
    const id = window.setInterval(load, 120_000);
    return () => window.clearInterval(id);
  }, [lat, lon]);

  return (
    <section className="mb-5" aria-labelledby="live-strip-title">
      <div className="flex items-center justify-between gap-2 mb-2 lg:flex-col lg:items-start lg:gap-1">
        <h2 id="live-strip-title" className="flex items-center gap-1.5 text-base font-bold text-text whitespace-nowrap">
          <span className="relative flex w-2.5 h-2.5" aria-hidden="true">
            {items.length > 0 && <span className="absolute inline-flex h-full w-full rounded-full bg-red-500 opacity-60 animate-ping motion-reduce:animate-none" />}
            <span className={`relative inline-flex rounded-full w-2.5 h-2.5 ${items.length > 0 ? 'bg-red-500' : 'bg-border'}`} />
          </span>
          {t('strip.title')}
        </h2>
        <Link to="/live/new" className="inline-flex items-center gap-1 text-xs font-semibold text-primary hover:underline">
          <Plus size={14} /> {t('strip.cta')}
        </Link>
      </div>

      {/* Telefono: striscia orizzontale; schermi larghi (colonna laterale della Home): elenco verticale */}
      {items.length > 0 && (
        <div className="flex gap-3 overflow-x-auto scrollbar-hide -mx-4 px-4 pb-1 snap-x lg:flex-col lg:overflow-visible lg:mx-0 lg:px-0">
          {items.map((b) => (
            <Link
              key={b.id}
              to={`/live/${b.id}`}
              className="snap-start shrink-0 w-64 lg:w-auto bg-white rounded-2xl p-3 border border-border/60 shadow-sm hover:border-primary/40 hover:shadow-md transition-all focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
            >
              <div className="flex items-center gap-2">
                <div className="w-9 h-9 rounded-full bg-primary/10 text-primary font-bold flex items-center justify-center shrink-0">
                  {b.store ? <Radio size={16} /> : (b.author.username[0] ?? '?').toUpperCase()}
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-semibold text-text truncate">
                    {b.isMine ? t('strip.you') : b.store ? b.store.name : `@${b.author.username}`}
                  </p>
                  <p className="text-[11px] text-text-muted">
                    {b.distanceKm != null && <>{t('strip.km', { km: b.distanceKm })} · </>}
                    {t('strip.until', { time: time(b.endsAt) })}
                  </p>
                </div>
              </div>
              <p className="mt-2 text-sm text-text flex items-start gap-1">
                <MapPin size={14} className="text-primary mt-0.5 shrink-0" />
                <span className="line-clamp-2">
                  {b.placeName}
                  {b.spotDetail && <span className="text-text-secondary"> · {b.spotDetail}</span>}
                </span>
              </p>
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}
