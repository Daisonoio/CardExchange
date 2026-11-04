import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Crown, Loader2, Lock, MapPin, Search, SlidersHorizontal, Star } from 'lucide-react';
import { advancedSearch, billing } from '../api';
import { useGame } from '../context/GameContext';
import type { AdvancedSearchCard, AdvancedSearchParams, AdvancedSearchResponse, CardFinish } from '../types';
import { CARD_LANGUAGES, CONDITION_LABELS, FINISH_LABELS, finishValue, languageShort } from '../types';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import PlanBadge from '../components/users/PlanBadge';

const euro = (v: number) => v.toLocaleString('it-IT', { style: 'currency', currency: 'EUR' });
const CONDITION_BY_NAME: Record<string, number> = {
  Mint: 1, NearMint: 2, Excellent: 3, Good: 4, LightlyPlayed: 5, ModeratelyPlayed: 6, HeavilyPlayed: 7, Damaged: 8,
};
const RADII = [5, 10, 25, 50, 100];
const PAGE_SIZE = 30;
const selectCls = 'w-full px-3 py-2.5 rounded-xl border border-border bg-white text-sm text-text';

type Filters = {
  searchTerm: string;
  gameId: string;
  language: string;
  finish: string;
  maxCondition: string;
  minPrice: string;
  maxPrice: string;
  radiusKm: string;
  sortBy: 'distance' | 'price' | 'newest';
};

const EMPTY: Filters = { searchTerm: '', gameId: '', language: '', finish: '', maxCondition: '', minPrice: '', maxPrice: '', radiusKm: '25', sortBy: 'distance' };

function toParams(f: Filters, page: number): AdvancedSearchParams {
  const num = (s: string) => {
    const v = Number(s.replace(',', '.'));
    return s.trim() && Number.isFinite(v) ? v : undefined;
  };
  return {
    searchTerm: f.searchTerm.trim() || undefined,
    gameId: num(f.gameId),
    language: f.language || undefined,
    finish: f.finish ? (Number(f.finish) as CardFinish) : undefined,
    maxCondition: num(f.maxCondition),
    minPrice: num(f.minPrice),
    maxPrice: num(f.maxPrice),
    radiusKm: num(f.radiusKm),
    sortBy: f.sortBy,
    page,
    pageSize: PAGE_SIZE,
  };
}

function ResultCard({ c }: { c: AdvancedSearchCard }) {
  const finish = finishValue(c.finish);
  const condition = CONDITION_BY_NAME[c.condition] as keyof typeof CONDITION_LABELS | undefined;
  return (
    <li className="flex gap-3 p-3 rounded-2xl bg-white border border-border">
      {c.imageUrl ? (
        <img src={c.imageUrl} alt="" loading="lazy" className="w-14 h-[78px] rounded-lg object-cover bg-surface-dark shrink-0" />
      ) : (
        <span className="w-14 h-[78px] rounded-lg bg-surface-dark shrink-0" />
      )}
      <div className="flex-1 min-w-0 space-y-0.5">
        <p className="text-sm font-semibold text-text truncate">{c.cardName}</p>
        <p className="text-[11px] text-text-muted truncate">{c.game} · {c.cardSet}{c.cardNumber ? ` #${c.cardNumber}` : ''}</p>
        <p className="text-[11px] text-text-secondary">
          {condition ? CONDITION_LABELS[condition] : c.condition}
          {c.language && ` · ${languageShort(c.language) ?? c.language.toUpperCase()}`}
          {finish && finish !== 1 && ` · ${FINISH_LABELS[finish]}`}
          {c.quantity > 1 && ` · ×${c.quantity}`}
        </p>
        <Link to={`/collection/${c.owner.id}`} className="flex items-center gap-1 text-xs text-text hover:underline min-w-0">
          <span className="truncate">@{c.owner.username}</span>
          <PlanBadge badge={c.owner.planBadge} />
          {c.owner.totalTradesCompleted > 0 && (
            <span className="inline-flex items-center gap-0.5 text-text-muted shrink-0"><Star size={10} className="text-amber-500" />{c.owner.reputationScore.toFixed(1)}</span>
          )}
        </Link>
        <p className="text-[11px] text-text-muted flex items-center gap-0.5">
          <MapPin size={10} />
          {c.distanceKm != null ? `${c.distanceKm} km` : ''}{c.location?.city ? `${c.distanceKm != null ? ' · ' : ''}${c.location.city}` : ''}
        </p>
      </div>
      <div className="flex flex-col items-end justify-between shrink-0">
        {c.price != null ? (
          <div className="text-right">
            <p className="text-sm font-bold text-text">{euro(c.price)}</p>
            <p className="text-[10px] text-text-muted">{c.askingPrice != null ? 'prezzo indicato' : 'valore di mercato'}</p>
          </div>
        ) : <span className="text-[10px] text-text-muted">senza prezzo</span>}
        <Link to={`/trades/new/${c.owner.id}`} className="text-xs font-semibold text-primary hover:underline">Proponi</Link>
      </div>
    </li>
  );
}

/** Ricerca avanzata (Premium): carte in scambio vicino a te con filtri su lingua, finitura, condizione e prezzo. */
export default function SearchPage() {
  const navigate = useNavigate();
  const { games } = useGame();
  const [premium, setPremium] = useState<boolean | null>(null);
  const [filters, setFilters] = useState<Filters>(EMPTY);
  const [result, setResult] = useState<AdvancedSearchResponse | null>(null);
  const [items, setItems] = useState<AdvancedSearchCard[]>([]);
  const [loading, setLoading] = useState(false);
  const [showFilters, setShowFilters] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    billing.current().then(({ data }) => setPremium(data.tier !== 'Free')).catch(() => setPremium(false));
  }, []);

  const run = async (page = 1, f = filters) => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await advancedSearch.search(toParams(f, page));
      setResult(data);
      setItems((prev) => (page === 1 ? data.cards : [...prev, ...data.cards]));
      if (page === 1) setShowFilters(false);
    } catch (err) {
      setError(apiErrorMessage(err, 'Ricerca non riuscita.'));
    } finally {
      setLoading(false);
    }
  };

  const set = <K extends keyof Filters>(k: K, v: Filters[K]) => setFilters((f) => ({ ...f, [k]: v }));

  const header = (
    <div className="flex items-center gap-3">
      <button onClick={() => navigate(-1)} aria-label="Indietro" className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0">
        <ArrowLeft size={18} />
      </button>
      <h1 className="text-xl font-bold text-text flex-1">Ricerca avanzata</h1>
    </div>
  );

  if (premium === null) {
    return <div className="max-w-lg mx-auto space-y-4">{header}<div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div></div>;
  }

  if (!premium) {
    return (
      <div className="max-w-lg mx-auto space-y-4">
        {header}
        <section className="bg-white rounded-2xl p-4 border-2 border-secondary space-y-2">
          <h2 className="text-base font-bold text-text flex items-center gap-1.5"><Lock size={16} className="text-amber-700" /> Funzione Premium</h2>
          <p className="text-sm text-text-secondary">
            Cerca tra le carte in scambio vicino a te per lingua, foil, condizione minima, fascia di prezzo e distanza, ordinando per vicinanza o prezzo.
          </p>
          <Link to="/premium" className="inline-flex items-center gap-1.5 mt-1 px-4 py-2.5 rounded-xl bg-primary text-white text-sm font-semibold">
            <Crown size={16} /> Scopri Premium
          </Link>
        </section>
      </div>
    );
  }

  const hasMore = result != null && items.length < result.totalCount;

  return (
    <div className="max-w-lg mx-auto pb-24 space-y-4">
      {header}

      <form onSubmit={(e) => { e.preventDefault(); run(1); }} className="space-y-3">
        <div className="flex gap-2">
          <div className="relative flex-1">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
            <input
              value={filters.searchTerm}
              onChange={(e) => set('searchTerm', e.target.value)}
              placeholder="Nome della carta o dell'espansione"
              aria-label="Cerca"
              className="w-full pl-9 pr-3 py-2.5 rounded-xl border border-border bg-white text-sm"
            />
          </div>
          <button
            type="button"
            onClick={() => setShowFilters((s) => !s)}
            aria-expanded={showFilters}
            aria-label="Filtri"
            className={`w-11 rounded-xl border flex items-center justify-center ${showFilters ? 'border-primary text-primary bg-primary/5' : 'border-border text-text-secondary bg-white'}`}
          >
            <SlidersHorizontal size={18} />
          </button>
        </div>

        {showFilters && (
          <div className="p-3 rounded-2xl bg-white border border-border grid grid-cols-2 gap-3">
            <label className="col-span-2 sm:col-span-1">
              <span className="block text-xs text-text-secondary mb-1">Gioco</span>
              <select value={filters.gameId} onChange={(e) => set('gameId', e.target.value)} className={selectCls}>
                <option value="">Tutti</option>
                {games.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
              </select>
            </label>
            <label>
              <span className="block text-xs text-text-secondary mb-1">Lingua</span>
              <select value={filters.language} onChange={(e) => set('language', e.target.value)} className={selectCls}>
                <option value="">Qualsiasi</option>
                {CARD_LANGUAGES.map((l) => <option key={l.code} value={l.code}>{l.label}</option>)}
              </select>
            </label>
            <label>
              <span className="block text-xs text-text-secondary mb-1">Finitura</span>
              <select value={filters.finish} onChange={(e) => set('finish', e.target.value)} className={selectCls}>
                <option value="">Qualsiasi</option>
                {Object.entries(FINISH_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </label>
            <label>
              <span className="block text-xs text-text-secondary mb-1">Condizione almeno</span>
              <select value={filters.maxCondition} onChange={(e) => set('maxCondition', e.target.value)} className={selectCls}>
                <option value="">Qualsiasi</option>
                {Object.entries(CONDITION_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </label>
            <label>
              <span className="block text-xs text-text-secondary mb-1">Distanza</span>
              <select value={filters.radiusKm} onChange={(e) => set('radiusKm', e.target.value)} className={selectCls}>
                {RADII.map((r) => <option key={r} value={r}>entro {r} km</option>)}
                <option value="">Ovunque</option>
              </select>
            </label>
            <label>
              <span className="block text-xs text-text-secondary mb-1">Prezzo da €</span>
              <input inputMode="decimal" value={filters.minPrice} onChange={(e) => set('minPrice', e.target.value)} className={selectCls} placeholder="0" />
            </label>
            <label>
              <span className="block text-xs text-text-secondary mb-1">Prezzo fino a €</span>
              <input inputMode="decimal" value={filters.maxPrice} onChange={(e) => set('maxPrice', e.target.value)} className={selectCls} placeholder="—" />
            </label>
            <label className="col-span-2">
              <span className="block text-xs text-text-secondary mb-1">Ordina per</span>
              <div className="flex rounded-xl border border-border overflow-hidden">
                {([['distance', 'Più vicine'], ['price', 'Prezzo più basso'], ['newest', 'Più recenti']] as const).map(([v, l]) => (
                  <button
                    key={v}
                    type="button"
                    onClick={() => set('sortBy', v)}
                    className={`flex-1 py-2 text-xs font-medium ${filters.sortBy === v ? 'bg-primary text-white' : 'bg-white text-text-secondary'}`}
                  >
                    {l}
                  </button>
                ))}
              </div>
            </label>
            <p className="col-span-2 text-[11px] text-text-muted">
              Il prezzo è quello indicato dal proprietario o, se manca, il valore di mercato. È indicativo: vi accordate tra voi.
            </p>
            <button type="button" onClick={() => setFilters(EMPTY)} className="text-xs text-text-secondary text-left">Azzera filtri</button>
          </div>
        )}

        <Button type="submit" className="w-full" isLoading={loading && items.length === 0}>
          <Search size={16} /> Cerca
        </Button>
      </form>

      {error && <div className="p-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}

      {result && (
        <>
          <p className="text-xs text-text-muted">
            {result.totalCount === 0 ? 'Nessuna carta trovata' : `${result.totalCount} carte trovate`}
            {!result.hasLocation && filters.radiusKm && ' · imposta la posizione nel profilo per filtrare per distanza'}
            {result.truncated && ' · troppi risultati: restringi la ricerca'}
          </p>
          {items.length === 0 ? (
            <EmptyState icon={Search} title="Nessun risultato" description="Prova ad allargare la distanza o a togliere qualche filtro." />
          ) : (
            <ul className="space-y-2">{items.map((c) => <ResultCard key={c.id} c={c} />)}</ul>
          )}
          {hasMore && (
            <Button variant="outline" className="w-full" onClick={() => run(result.page + 1)} isLoading={loading}>
              Altri risultati
            </Button>
          )}
        </>
      )}
    </div>
  );
}
