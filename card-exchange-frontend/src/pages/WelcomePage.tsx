import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { BellRing, Check, Crosshair, Heart, Loader2, MapPin, Search, Sparkles } from 'lucide-react';
import { users, wishlist } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import { reversePlace, searchPlaces, type Place } from '../utils/geocoding';
import { resetSearchRadius } from '../hooks/useSearchRadius';
import GameCardSearch, { importGameCard } from '../components/cards/GameCardSearch';
import type { CarouselCard } from '../components/cards/CardCarousel';
import type { GameCard } from '../types';
import Button from '../components/ui/Button';
import PushToggle from '../components/notifications/PushToggle';
import { useFormat } from '../i18n/format';

const TARGET_CARDS = 5;
const DEFAULT_RADIUS_KM = 25;
type Step = 'place' | 'cards' | 'result';
type Added = { cardInfoId: number; name: string; image?: string };

/**
 * Primo accesso in 2 minuti: dove scambi, 5 carte che cerchi, e subito chi le ha vicino a te
 * (oppure l'avviso che ti scriveremo appena qualcuno le ha).
 */
export default function WelcomePage() {
  const { t, i18n } = useTranslation('welcome');
  const { euro } = useFormat();
  const navigate = useNavigate();
  const { user, refreshUser } = useAuth();
  const { games, selectedGameId, setSelectedGameId } = useGame();
  const hasLocation = !!(user?.location?.latitude && user?.location?.longitude);
  const [step, setStep] = useState<Step>(hasLocation ? 'cards' : 'place');

  // Passo 1: posizione
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<Place[]>([]);
  const [place, setPlace] = useState<Place | null>(null);
  const [locating, setLocating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const debounce = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Passo 2: carte cercate
  const [added, setAdded] = useState<Added[]>([]);
  const [adding, setAdding] = useState(false);

  // Passo 3: risultato
  const [matches, setMatches] = useState<CarouselCard[] | null>(null);

  useEffect(() => {
    if (debounce.current) clearTimeout(debounce.current);
    if (place && query === place.label) return;
    debounce.current = setTimeout(() => {
      searchPlaces(query, i18n.language).then(setResults).catch(() => setResults([]));
    }, 350);
    return () => { if (debounce.current) clearTimeout(debounce.current); };
  }, [query, place, i18n.language]);

  if (!user) return null;

  const useGps = () => {
    if (!navigator.geolocation) { setError(t('place.gpsError')); return; }
    setLocating(true);
    setError(null);
    navigator.geolocation.getCurrentPosition(async (pos) => {
      const found = await reversePlace(pos.coords.latitude, pos.coords.longitude, i18n.language).catch(() => null);
      setLocating(false);
      if (!found) { setError(t('place.gpsError')); return; }
      setPlace(found);
      setQuery(found.label);
      setResults([]);
    }, () => { setLocating(false); setError(t('place.gpsError')); }, { timeout: 10000 });
  };

  const savePlace = async () => {
    if (!place) return;
    setSaving(true);
    setError(null);
    try {
      await users.updateLocation(user.id, {
        city: place.city, province: place.province || place.city, country: place.country,
        postalCode: place.postalCode, latitude: place.latitude, longitude: place.longitude, maxDistanceKm: DEFAULT_RADIUS_KM,
      });
      resetSearchRadius(user.id);
      await refreshUser();
      setStep('cards');
    } catch {
      setError(t('place.saveError'));
    } finally {
      setSaving(false);
    }
  };

  const addCard = async (card: GameCard) => {
    if (added.length >= TARGET_CARDS || adding) return;
    setAdding(true);
    setError(null);
    try {
      const game = games.find((g) => g.id === selectedGameId);
      const cardInfoId = await importGameCard(game?.name, card.externalId, game?.catalogKey);
      if (added.some((a) => a.cardInfoId === cardInfoId)) return;
      await wishlist.create(user.id, { cardInfoId, priority: 2 });
      setAdded((prev) => [...prev, { cardInfoId, name: card.name, image: card.imageSmall }]);
    } catch {
      setError(t('cards.addError'));
    } finally {
      setAdding(false);
    }
  };

  const showResult = async () => {
    setStep('result');
    try {
      const { data } = await wishlist.topNearbyMatches(user.id, { radiusKm: DEFAULT_RADIUS_KM });
      setMatches(((data as { cards?: CarouselCard[] }).cards ?? []).slice(0, 6));
    } catch {
      setMatches([]);
    }
  };

  const stepIndex = { place: 0, cards: 1, result: 2 }[step];

  return (
    <div className="pb-24 max-w-lg mx-auto space-y-5">
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-primary">{t('stepOf', { n: stepIndex + 1, total: 3 })}</p>
        <div className="mt-2 grid grid-cols-3 gap-1.5" aria-hidden="true">
          {[0, 1, 2].map((i) => <div key={i} className={`h-1.5 rounded-full ${i <= stepIndex ? 'bg-primary' : 'bg-border'}`} />)}
        </div>
      </div>

      {step === 'place' && (
        <section className="space-y-4">
          <div>
            <h1 className="text-2xl font-extrabold text-text text-balance">{t('place.title')}</h1>
            <p className="text-sm text-text-secondary mt-1">{t('place.subtitle')}</p>
          </div>
          <button type="button" onClick={useGps} disabled={locating} className="w-full flex items-center justify-center gap-2 py-3 rounded-xl bg-primary/10 text-primary font-semibold hover:bg-primary/15 disabled:opacity-60">
            {locating ? <Loader2 size={18} className="animate-spin" /> : <Crosshair size={18} />} {locating ? t('place.locating') : t('place.useGps')}
          </button>
          <div className="relative">
            <label htmlFor="welcome-city" className="block text-xs font-semibold text-text-secondary mb-1">{t('place.orCity')}</label>
            <div className="relative">
              <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted pointer-events-none" />
              <input id="welcome-city" value={query} onChange={(e) => { setQuery(e.target.value); setPlace(null); }} placeholder={t('place.cityPlaceholder')} autoComplete="off"
                className="w-full pl-9 pr-3 py-3 rounded-xl border border-border bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/30" />
            </div>
            {results.length > 0 && !place && (
              <ul className="absolute z-10 mt-1 w-full bg-white border border-border rounded-xl shadow-lg overflow-hidden" role="listbox">
                {results.map((r) => (
                  <li key={`${r.latitude},${r.longitude}`}>
                    <button type="button" onClick={() => { setPlace(r); setQuery(r.label); setResults([]); }} className="w-full text-left px-3 py-2.5 text-sm hover:bg-surface-dark flex items-center gap-2">
                      <MapPin size={14} className="text-text-muted shrink-0" /> {r.label}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
          {place && <p className="text-sm text-text flex items-center gap-1.5"><Check size={16} className="text-green-600" /> {t('place.chosen', { place: place.label })}</p>}
          <p className="text-xs text-text-muted">{t('place.privacy')}</p>
          {error && <p role="alert" className="text-sm text-danger">{error}</p>}
          <Button onClick={savePlace} disabled={!place} isLoading={saving} className="w-full" size="lg">{t('next')}</Button>
          <button type="button" onClick={() => navigate('/home')} className="w-full text-sm text-text-muted hover:underline">{t('skip')}</button>
        </section>
      )}

      {step === 'cards' && (
        <section className="space-y-4">
          <div>
            <h1 className="text-2xl font-extrabold text-text text-balance">{t('cards.title')}</h1>
            <p className="text-sm text-text-secondary mt-1">{t('cards.subtitle', { count: TARGET_CARDS })}</p>
          </div>
          <div className="flex flex-wrap gap-2" role="radiogroup" aria-label={t('cards.game')}>
            {games.map((g) => (
              <button key={g.id} type="button" role="radio" aria-checked={selectedGameId === g.id} onClick={() => setSelectedGameId(g.id)}
                className={`px-3 py-1.5 rounded-full text-sm font-semibold border ${selectedGameId === g.id ? 'bg-primary text-white border-primary' : 'bg-white text-text-secondary border-border'}`}>
                {g.name}
              </button>
            ))}
          </div>
          {selectedGameId ? (
            added.length < TARGET_CARDS && <GameCardSearch onSelect={addCard} placeholder={t('cards.searchPlaceholder')} />
          ) : (
            <p className="text-sm text-text-muted">{t('cards.pickGame')}</p>
          )}
          {adding && <p className="text-sm text-text-muted flex items-center gap-2"><Loader2 size={14} className="animate-spin" /> {t('cards.adding')}</p>}
          <div>
            <p className="text-xs font-semibold text-text-secondary mb-1.5">{t('cards.progress', { n: added.length, total: TARGET_CARDS })}</p>
            <ul className="space-y-1.5">
              {added.map((a) => (
                <li key={a.cardInfoId} className="flex items-center gap-2 bg-white border border-border rounded-xl p-2">
                  {a.image ? <img src={a.image} alt="" className="w-8 h-11 rounded object-cover" /> : <Heart size={16} className="text-primary mx-2" />}
                  <span className="text-sm font-medium text-text flex-1 truncate">{a.name}</span>
                  <Check size={16} className="text-green-600" />
                </li>
              ))}
            </ul>
          </div>
          {error && <p role="alert" className="text-sm text-danger">{error}</p>}
          <Button onClick={showResult} disabled={added.length === 0} className="w-full" size="lg">
            {added.length >= TARGET_CARDS ? t('cards.seeWhoHasThem') : t('cards.continueWith', { count: added.length })}
          </Button>
          <button type="button" onClick={() => navigate('/home')} className="w-full text-sm text-text-muted hover:underline">{t('skip')}</button>
        </section>
      )}

      {step === 'result' && (
        <section className="space-y-4">
          {matches === null && <div className="flex justify-center py-12"><Loader2 className="animate-spin text-primary" /></div>}
          {matches && matches.length > 0 && (
            <>
              <div>
                <h1 className="text-2xl font-extrabold text-text text-balance flex items-center gap-2"><Sparkles className="text-primary" /> {t('result.foundTitle', { count: matches.length })}</h1>
                <p className="text-sm text-text-secondary mt-1">{t('result.foundSubtitle', { km: DEFAULT_RADIUS_KM })}</p>
              </div>
              <ul className="space-y-2">
                {matches.map((m) => (
                  <li key={m.cardId}>
                    <Link to={`/collection/${m.ownerId}`} className="flex items-center gap-3 bg-white border border-border rounded-xl p-2.5 hover:border-primary/40">
                      {m.imageUrl ? <img src={m.imageUrl} alt="" className="w-10 h-14 rounded object-cover" /> : <div className="w-10 h-14 rounded bg-surface-dark" />}
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm font-semibold text-text truncate">{m.cardName}</span>
                        <span className="block text-xs text-text-secondary">@{m.ownerUsername} · {t('result.km', { km: Math.max(1, Math.round(m.distanceKm)) })}{m.priceEur ? ` · ${euro(m.priceEur)}` : ''}</span>
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            </>
          )}
          {matches && matches.length === 0 && (
            <div className="space-y-3">
              <h1 className="text-2xl font-extrabold text-text text-balance flex items-center gap-2"><BellRing className="text-primary" /> {t('result.emptyTitle')}</h1>
              <p className="text-sm text-text-secondary">{t('result.emptySubtitle')}</p>
              <div className="bg-white border border-border rounded-2xl p-3"><PushToggle /></div>
            </div>
          )}
          {matches && (
            <div className="grid gap-2">
              <Button onClick={() => navigate('/home')} size="lg" className="w-full">{t('result.goHome')}</Button>
              <Link to="/explore" className="text-center text-sm font-semibold text-primary hover:underline">{t('result.explore')}</Link>
            </div>
          )}
        </section>
      )}

    </div>
  );
}
