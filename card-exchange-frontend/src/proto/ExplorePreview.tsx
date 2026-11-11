import { useCallback, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeftRight, ChevronDown, Loader2, Lock, MapPin, Search, SlidersHorizontal, X } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import { apiErrorMessage } from '../utils/apiError';
import { explore, type ExploreCard, type ExploreFilters, type ExploreResponse } from './api';
import { CardTile, ThemeToggle } from './parts';
import { useEntitlements } from '../hooks/useEntitlements';

const RADII = [1, 5, 10, 25, 50, 100];
const CONDITIONS = [{ value: 2, key: 'nm' }, { value: 3, key: 'ex' }, { value: 4, key: 'gd' }, { value: 5, key: 'lp' }] as const;

/** Anteprima: Esplora con griglia di carte, distanza indicativa e filtri in un pannello. */
export default function ExplorePreview() {
  const { t } = useTranslation('proto');
  const { user } = useAuth();
  const { games } = useGame();
  const [params] = useSearchParams();
  const initialQuery = params.get('q') ?? '';
  const [filters, setFilters] = useState<ExploreFilters>({ radiusKm: 25, sort: 'distance', q: initialQuery || undefined });
  const [query, setQuery] = useState(initialQuery);
  const [data, setData] = useState<ExploreResponse | null>(null);
  const [cards, setCards] = useState<ExploreCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [sheet, setSheet] = useState(false);

  const load = useCallback(async (f: ExploreFilters, page = 1) => {
    setLoading(true);
    setError(null);
    try {
      const { data: res } = await explore.cards({ ...f, page, pageSize: 24 });
      setData(res);
      setCards((prev) => (page === 1 ? res.cards : [...prev, ...res.cards]));
    } catch (err) {
      setError(apiErrorMessage(err, t('errors.load')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { load(filters); }, [filters, load]);

  // Ricerca testuale con un piccolo ritardo, per non interrogare a ogni lettera
  useEffect(() => {
    const id = setTimeout(() => setFilters((f) => (f.q === (query.trim() || undefined) ? f : { ...f, q: query.trim() || undefined })), 350);
    return () => clearTimeout(id);
  }, [query]);

  const activeAdvanced = [filters.minPrice, filters.maxPrice, filters.maxCondition, filters.finish].filter((v) => v != null).length;

  return (
    <div className="px-4 pt-4">
      {/* Intestazione: zona e tema */}
      <div className="flex items-center gap-2 mb-4">
        <span className="cx-chip inline-flex items-center gap-1.5 px-3 py-2 text-sm font-semibold" style={{ color: 'var(--cx-text)' }}>
          <MapPin size={15} className="cx-accent-text" />
          {user?.location?.city ?? t('noLocation')}
        </span>
        <span className="flex-1" />
        <Link to="/explore" className="text-[11px] cx-muted underline underline-offset-2">{t('backToCurrent')}</Link>
        <ThemeToggle />
      </div>

      <h1 className="text-[26px] leading-[1.1] font-extrabold tracking-tight mb-4">
        {t('explore.titleLine1')}<br /><span className="cx-accent-text">{t('explore.titleLine2')}</span>
      </h1>

      {/* Ricerca + filtri */}
      <div className="flex gap-2 mb-3">
        <label className="cx-input flex-1 flex items-center gap-2 px-3.5 h-12">
          <Search size={18} className="cx-muted shrink-0" />
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('explore.searchPlaceholder')}
            className="bg-transparent outline-none flex-1 text-[15px] min-w-0" aria-label={t('explore.searchPlaceholder')} />
          {query && <button onClick={() => setQuery('')} aria-label={t('clear')}><X size={16} className="cx-muted" /></button>}
        </label>
        <button onClick={() => setSheet(true)} aria-label={t('filters.title')}
          className="relative w-12 h-12 rounded-2xl flex items-center justify-center" style={{ background: 'var(--cx-accent)', color: 'var(--cx-on-accent)' }}>
          <SlidersHorizontal size={20} />
          {activeAdvanced > 0 && <span className="absolute -top-1 -right-1 w-5 h-5 rounded-full text-[10px] font-bold flex items-center justify-center" style={{ background: 'var(--cx-text)', color: 'var(--cx-bg)' }}>{activeAdvanced}</span>}
        </button>
      </div>

      {/* Giochi */}
      <div className="flex gap-2 overflow-x-auto pb-1 mb-3 -mx-4 px-4" style={{ scrollbarWidth: 'none' }}>
        <button className="cx-chip px-4 py-2 text-sm font-semibold whitespace-nowrap" aria-pressed={!filters.gameId}
          onClick={() => setFilters((f) => ({ ...f, gameId: undefined }))}>{t('explore.allGames')}</button>
        {games.map((g) => (
          <button key={g.id} className="cx-chip px-4 py-2 text-sm font-semibold whitespace-nowrap" aria-pressed={filters.gameId === g.id}
            onClick={() => setFilters((f) => ({ ...f, gameId: g.id }))}>{g.name}</button>
        ))}
      </div>

      {/* Conteggio e ordinamento */}
      <div className="flex items-center justify-between mb-3 text-sm">
        <span className="font-semibold">{data ? t('explore.results', { count: data.total }) : ' '}</span>
        <label className="relative inline-flex items-center gap-1 cx-text-2 font-semibold">
          <select value={filters.sort} onChange={(e) => setFilters((f) => ({ ...f, sort: e.target.value as ExploreFilters['sort'] }))}
            className="appearance-none bg-transparent pr-5 outline-none cursor-pointer" aria-label={t('sort.label')}>
            <option value="distance">{t('sort.distance')}</option>
            <option value="price">{t('sort.price')}</option>
            <option value="newest">{t('sort.newest')}</option>
          </select>
          <ChevronDown size={14} className="absolute right-0 pointer-events-none" />
        </label>
      </div>

      {error && <div className="cx-card p-3 text-sm mb-3" style={{ color: 'var(--cx-danger)' }}>{error}</div>}
      {data && !data.hasLocation && (
        <Link to="/profile/edit" className="cx-card p-3 text-sm mb-3 flex items-center gap-2">
          <MapPin size={16} className="cx-accent-text shrink-0" /> {t('explore.setLocation')}
        </Link>
      )}

      {loading && cards.length === 0 ? (
        <div className="grid grid-cols-2 gap-3">
          {Array.from({ length: 4 }, (_, i) => <div key={i} className="cx-card aspect-[63/110] animate-pulse" />)}
        </div>
      ) : cards.length === 0 ? (
        <div className="cx-card p-6 text-center">
          <ArrowLeftRight size={28} className="mx-auto cx-accent-text mb-2" />
          <p className="font-bold">{t('explore.emptyTitle')}</p>
          <p className="text-sm cx-muted mt-1">{t('explore.emptyBody')}</p>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3">{cards.map((c) => <CardTile key={c.id} card={c} />)}</div>
          {data?.hasMore && (
            <button onClick={() => load(filters, (data?.page ?? 1) + 1)} disabled={loading}
              className="cx-btn-outline w-full h-12 mt-4 flex items-center justify-center gap-2">
              {loading && <Loader2 size={16} className="animate-spin" />}{t('showMore')}
            </button>
          )}
        </>
      )}

      <p className="text-[11px] cx-muted text-center mt-5 px-4">{t('explore.priceNote')}</p>

      {sheet && <FilterSheet initial={filters} isPremium={!!data?.isPremium} onClose={() => setSheet(false)}
        onApply={(f) => { setFilters(f); setSheet(false); }} />}
    </div>
  );
}

function FilterSheet({ initial, isPremium, onClose, onApply }: {
  initial: ExploreFilters; isPremium: boolean; onClose: () => void; onApply: (f: ExploreFilters) => void;
}) {
  const { paidPlans } = useEntitlements();
  const { t } = useTranslation('proto');
  const [f, setF] = useState<ExploreFilters>(initial);
  const radiusIndex = Math.max(0, RADII.indexOf(f.radiusKm ?? 25));
  const num = (s: string) => { const v = Number(s.replace(',', '.')); return s.trim() && Number.isFinite(v) ? v : undefined; };

  return (
    <div className="fixed inset-0 z-[600] flex items-end justify-center" role="dialog" aria-modal="true" aria-label={t('filters.title')}>
      <button className="absolute inset-0 bg-black/50" onClick={onClose} aria-label={t('close')} />
      <div className="cx-sheet relative w-full max-w-md p-5 pb-8 max-h-[88dvh] overflow-y-auto">
        <div className="w-10 h-1.5 rounded-full mx-auto mb-4" style={{ background: 'var(--cx-border)' }} />
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-xl font-extrabold">{t('filters.title')}</h2>
          <button onClick={onClose} aria-label={t('close')}><X size={22} /></button>
        </div>

        <section className="mb-6">
          <div className="flex justify-between mb-2">
            <span className="font-bold">{t('filters.distance')}</span>
            <span className="font-bold cx-accent-text inline-flex items-center gap-1"><MapPin size={14} />{t('filters.within', { km: RADII[radiusIndex] })}</span>
          </div>
          <input type="range" min={0} max={RADII.length - 1} step={1} value={radiusIndex} className="w-full cx-range"
            onChange={(e) => setF({ ...f, radiusKm: RADII[Number(e.target.value)] })} aria-label={t('filters.distance')} />
          <div className="flex justify-between text-[11px] cx-muted mt-1">{RADII.map((r) => <span key={r}>{r} km</span>)}</div>
        </section>

        {(isPremium || paidPlans) && (
        <section className={`mb-6 ${isPremium ? '' : 'opacity-60'}`} aria-disabled={!isPremium}>
          <div className="flex items-center gap-2 mb-3">
            <span className="font-bold">{t('filters.price')}</span>
            {!isPremium && <span className="cx-badge text-[10px] px-2 py-0.5 inline-flex items-center gap-1"><Lock size={10} />Premium</span>}
          </div>
          <div className="flex items-center gap-2">
            <input disabled={!isPremium} inputMode="decimal" placeholder={t('filters.min')} defaultValue={f.minPrice ?? ''}
              onChange={(e) => setF({ ...f, minPrice: num(e.target.value) })} className="cx-input h-11 px-3 w-full" aria-label={t('filters.min')} />
            <span className="cx-muted">–</span>
            <input disabled={!isPremium} inputMode="decimal" placeholder={t('filters.max')} defaultValue={f.maxPrice ?? ''}
              onChange={(e) => setF({ ...f, maxPrice: num(e.target.value) })} className="cx-input h-11 px-3 w-full" aria-label={t('filters.max')} />
          </div>
        </section>
        )}

        <section className={`mb-6 ${isPremium ? '' : 'opacity-60'}`}>
          <p className="font-bold mb-3">{t('filters.condition')}</p>
          <div className="flex flex-wrap gap-2">
            {CONDITIONS.map((c) => (
              <button key={c.key} disabled={!isPremium} className="cx-chip px-3.5 py-2 text-sm font-semibold" aria-pressed={f.maxCondition === c.value}
                onClick={() => setF({ ...f, maxCondition: f.maxCondition === c.value ? undefined : c.value })}>{t(`filters.conditions.${c.key}`)}</button>
            ))}
            <button disabled={!isPremium} className="cx-chip px-3.5 py-2 text-sm font-semibold" aria-pressed={f.finish === 2}
              onClick={() => setF({ ...f, finish: f.finish === 2 ? undefined : 2 })}>{t('filters.foilOnly')}</button>
          </div>
          {!isPremium && paidPlans && <Link to="/premium" className="block text-sm font-semibold mt-3 cx-accent-text underline underline-offset-2">{t('filters.unlock')}</Link>}
        </section>

        <button onClick={() => onApply(f)} className="cx-btn w-full h-13 py-3.5 text-base">{t('filters.apply')}</button>
        <button onClick={() => onApply({ radiusKm: 25, sort: initial.sort, q: initial.q, gameId: initial.gameId })} className="w-full mt-3 text-sm font-semibold cx-text-2">{t('filters.reset')}</button>
      </div>
    </div>
  );
}
