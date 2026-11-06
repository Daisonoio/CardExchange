import { Fragment, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Heart, Loader2, MapPin, Store } from 'lucide-react';
import { explore, type ExploreCard } from '../../proto/api';
import { useFormat } from '../../i18n/format';
import SponsoredSlotCard from '../promotions/SponsoredSlotCard';

const PAGE_SIZE = 12;

interface Props {
  gameId: number | null;
  radiusKm: number;
  /** Posizione del dispositivo, solo se nel profilo non c'è la zona. */
  coords: { latitude: number; longitude: number } | null;
  /** Avvisa la Home di quante carte ci sono (per scegliere cosa mostrare se è vuota). */
  onTotal?: (total: number) => void;
}

/** Una carta della griglia: immagine, distanza arrotondata, prezzo solo come riferimento. */
function Tile({ card }: { card: ExploreCard }) {
  const { t } = useTranslation('home');
  const { euro } = useFormat();
  return (
    <Link to={`/collection/${card.owner.id}`} state={{ preselectCardId: card.id, preselectCardInfoId: card.cardInfoId }}
      className="bg-white rounded-2xl border border-border/60 overflow-hidden flex flex-col hover:border-primary/40">
      <div className="relative aspect-[63/88] bg-surface-dark">
        {card.imageUrl
          ? <img src={card.imageUrl} alt="" loading="lazy" className="w-full h-full object-cover" />
          : <span className="absolute inset-0 grid place-items-center p-2 text-xs text-text-muted text-center">{card.name}</span>}
        {card.approxKm != null && (
          <span className="absolute left-1.5 bottom-1.5 inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded-full bg-black/65 text-white text-[10px] font-semibold">
            <MapPin size={10} /> {t('all.km', { km: card.approxKm })}
          </span>
        )}
        {card.isWanted && (
          <span className="absolute right-1.5 top-1.5 w-6 h-6 rounded-full grid place-items-center bg-black/55" title={t('all.wanted')} aria-label={t('all.wanted')}>
            <Heart size={12} fill="#ff6b7a" stroke="#ff6b7a" />
          </span>
        )}
      </div>
      <div className="p-2 flex-1 flex flex-col gap-0.5">
        <p className="text-xs font-bold text-text line-clamp-1">{card.name}</p>
        <p className="text-[10px] text-text-muted line-clamp-1 flex items-center gap-1">
          {card.owner.planBadge === 'Store' && <Store size={10} className="shrink-0" aria-label={t('all.store')} />}
          @{card.owner.username}
        </p>
        <p className="mt-auto text-xs font-semibold text-text">
          {card.price != null ? euro(card.price) : '—'}
          {card.price != null && <span className="ml-1 text-[10px] font-normal text-text-muted">{card.priceKind === 'asking' ? t('all.asking') : t('all.market')}</span>}
        </p>
      </div>
    </Link>
  );
}

/**
 * Tutte le carte disponibili vicino all'utente, dalla più vicina: negozi e privati insieme,
 * ordinati solo per distanza (nessun confronto di prezzo tra venditori).
 */
export default function NearbyCardsGrid({ gameId, radiusKm, coords, onTotal }: Props) {
  const { t } = useTranslation('home');
  const [cards, setCards] = useState<ExploreCard[]>([]);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const lat = coords?.latitude;
  const lng = coords?.longitude;

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      try {
        const { data } = await explore.cards({
          gameId: gameId ?? undefined, radiusKm, sort: 'distance', page, pageSize: PAGE_SIZE,
          ...(lat != null && lng != null ? { latitude: lat, longitude: lng } : {}),
        });
        if (cancelled) return;
        setCards((prev) => (page === 1 ? data.cards : [...prev, ...data.cards]));
        setHasMore(data.hasMore);
        setTotal(data.total);
        if (page === 1) onTotal?.(data.total);
      } catch {
        if (!cancelled && page === 1) { setCards([]); onTotal?.(0); }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, [gameId, radiusKm, lat, lng, page, onTotal]);

  // Filtri cambiati: si riparte dalla prima pagina
  const [filters, setFilters] = useState({ gameId, radiusKm, lat, lng });
  if (filters.gameId !== gameId || filters.radiusKm !== radiusKm || filters.lat !== lat || filters.lng !== lng) {
    setFilters({ gameId, radiusKm, lat, lng });
    setPage(1);
  }

  if (!loading && cards.length === 0) return null;

  return (
    <section aria-labelledby="home-all-cards" className="mt-6">
      <div className="flex items-baseline justify-between mb-3">
        <h2 id="home-all-cards" className="text-base font-bold text-text">{t('all.title')}</h2>
        {total > 0 && <span className="text-xs text-text-muted">{t('all.count', { count: total })}</span>}
      </div>
      <p className="text-xs text-text-muted -mt-2 mb-3">{t('all.hint')}</p>
      <ul className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3" data-testid="home-all-cards">
        {cards.map((c, i) => (
          <Fragment key={c.id}>
            <li><Tile card={c} /></li>
            {/* Al massimo uno spazio sponsorizzato, dopo l'ottava carta */}
            {i === 7 && <li className="col-span-full"><SponsoredSlotCard placement="HomeFeed" coords={coords} gameId={gameId} /></li>}
          </Fragment>
        ))}
      </ul>
      {loading ? (
        <div className="flex justify-center py-6"><Loader2 className="animate-spin text-primary" /></div>
      ) : hasMore && (
        <button type="button" onClick={() => setPage((p) => p + 1)}
          className="mt-4 w-full py-2.5 rounded-xl border border-border bg-white text-sm font-semibold text-primary hover:border-primary/40">
          {t('all.more')}
        </button>
      )}
    </section>
  );
}
