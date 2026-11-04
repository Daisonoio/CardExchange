import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Sparkles, MapPin, Star, ArrowLeftRight, ChevronDown, ChevronUp,
  Loader2, RefreshCw, SlidersHorizontal, Users, ArrowRight, Trophy, Check
} from 'lucide-react';
import { matchmaking } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import type { MatchResult, MatchCard } from '../types';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import PlanBadge from '../components/users/PlanBadge';
import { useFormat } from '../i18n/format';
import { useSearchRadius } from '../hooks/useSearchRadius';
import RadiusSlider from '../components/ui/RadiusSlider';

export default function MatchmakingPage() {
  const { t } = useTranslation('matchmaking');
  const { user } = useAuth();
  const { selectedGameId } = useGame();
  const navigate = useNavigate();

  const [matches, setMatches] = useState<MatchResult[]>([]);
  const [totalMatches, setTotalMatches] = useState(0);
  const [mutualCount, setMutualCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  // Stesso raggio di Esplora e delle altre sezioni
  const [radiusKm, setRadiusKm] = useSearchRadius();
  // La ricerca parte quando si smette di muovere il cursore
  const [queryRadius, setQueryRadius] = useState(radiusKm);
  useEffect(() => {
    const id = setTimeout(() => setQueryRadius(radiusKm), 300);
    return () => clearTimeout(id);
  }, [radiusKm]);
  const [showFilters, setShowFilters] = useState(false);
  const [filterMutualOnly, setFilterMutualOnly] = useState(false);
  // Il server restituisce i migliori match a pagine da 50
  const [limit, setLimit] = useState(50);
  const [hasMore, setHasMore] = useState(false);

  // Expanded cards per match
  const [expandedMatch, setExpandedMatch] = useState<number | null>(null);

  const loadMatches = useCallback(async () => {
    if (!user) return;
    setIsLoading(true);
    setError(null);
    try {
      const { data } = await matchmaking.getMatches({ radiusKm: queryRadius, gameId: selectedGameId, limit, mutualOnly: filterMutualOnly });
      setHasMore(!!data?.hasMore);
      const list = data?.matches ?? [];
      setMatches(Array.isArray(list) ? list : []);
      setTotalMatches(data?.totalMatches ?? 0);
      setMutualCount(data?.mutualMatches ?? 0);
    } catch (err: any) {
      const msg = err?.response?.data?.message;
      if (msg === 'NO_LOCATION') {
        setError(t('errors.noLocation'));
      } else {
        setError(t('errors.loadFailed'));
      }
    } finally {
      setIsLoading(false);
    }
  }, [user, queryRadius, selectedGameId, limit, filterMutualOnly, t]);

  useEffect(() => { loadMatches(); }, [loadMatches]);

  const displayedMatches = filterMutualOnly ? matches.filter(m => m.isMutual) : matches;

  // Le carte scelte arrivano nella proposta già come carte richieste
  const handleStartTrade = (userId: number, requestedCardIds: number[]) => {
    navigate(`/trades/new/${userId}`, { state: { requestedCardIds } });
  };

  return (
    <div className="max-w-4xl mx-auto">
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold text-text flex items-center gap-2">
            <Sparkles size={22} className="text-primary" />
            Matchmaking
          </h1>
          <p className="text-sm text-text-secondary mt-0.5">
            {t('subtitle')}
          </p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => setShowFilters(!showFilters)}
            className={`p-2 rounded-xl border transition-colors ${
              showFilters ? 'border-primary bg-primary/10 text-primary' : 'border-border text-text-muted hover:bg-surface-dark'
            }`}
          >
            <SlidersHorizontal size={18} />
          </button>
          <button
            onClick={loadMatches}
            disabled={isLoading}
            className="p-2 rounded-xl border border-border text-text-muted hover:bg-surface-dark transition-colors"
          >
            <RefreshCw size={18} className={isLoading ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      {/* Filters panel */}
      {showFilters && (
        <div className="bg-white rounded-2xl p-4 border border-border/50 shadow-sm mb-4 space-y-3">
          <div>
            <label htmlFor="match-radius" className="text-xs font-semibold text-text-secondary block mb-1">
              {t('filters.radiusLabel')}
            </label>
            <RadiusSlider id="match-radius" value={radiusKm} onChange={setRadiusKm} />
          </div>
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-text-secondary">{t('filters.mutualOnly')}</span>
            <button
              onClick={() => { setFilterMutualOnly(!filterMutualOnly); setLimit(50); }}
              className={`w-10 h-6 rounded-full transition-colors relative ${
                filterMutualOnly ? 'bg-primary' : 'bg-gray-300'
              }`}
            >
              <div className={`w-4 h-4 bg-white rounded-full absolute top-1 transition-transform ${
                filterMutualOnly ? 'translate-x-5' : 'translate-x-1'
              }`} />
            </button>
          </div>
        </div>
      )}

      {/* Stats bar */}
      {!isLoading && !error && matches.length > 0 && (
        <div className="grid grid-cols-3 gap-2 mb-4">
          <div className="bg-white rounded-xl p-3 border border-border/50 text-center">
            <p className="text-lg font-bold text-text">{totalMatches}</p>
            <p className="text-[10px] text-text-muted">{t('stats.found')}</p>
          </div>
          <div className="bg-primary/5 rounded-xl p-3 border border-primary/20 text-center">
            <p className="text-lg font-bold text-primary">{mutualCount}</p>
            <p className="text-[10px] text-primary/70">{t('stats.mutual')}</p>
          </div>
          <div className="bg-white rounded-xl p-3 border border-border/50 text-center">
            <p className="text-lg font-bold text-text">{radiusKm} km</p>
            <p className="text-[10px] text-text-muted">{t('stats.radius')}</p>
          </div>
        </div>
      )}

      {/* Content */}
      {isLoading ? (
        <div className="flex flex-col items-center justify-center py-16 gap-3">
          <Loader2 size={32} className="animate-spin text-primary" />
          <p className="text-sm text-text-muted">{t('loading')}</p>
        </div>
      ) : error ? (
        <div className="bg-red-50 border border-red-200 rounded-2xl p-6 text-center">
          <p className="text-sm text-red-700">{error}</p>
          {error === t('errors.noLocation') && (
            <Button onClick={() => navigate('/profile/edit')} size="sm" className="mt-3">
              {t('errors.setLocation')}
            </Button>
          )}
        </div>
      ) : displayedMatches.length === 0 ? (
        <EmptyState
          icon={Users}
          title={filterMutualOnly ? t('empty.mutualTitle') : t('empty.title')}
          description={
            filterMutualOnly
              ? t('empty.mutualDescription')
              : t('empty.description')
          }
          action={
            filterMutualOnly ? (
              <Button onClick={() => setFilterMutualOnly(false)} size="sm" variant="outline">
                {t('empty.showAll')}
              </Button>
            ) : undefined
          }
        />
      ) : (
        <div className="space-y-3">
          {displayedMatches.map((match) => (
            <MatchResultCard
              key={match.userId}
              match={match}
              isExpanded={expandedMatch === match.userId}
              onToggle={() => setExpandedMatch(expandedMatch === match.userId ? null : match.userId)}
              onStartTrade={(ids) => handleStartTrade(match.userId, ids)}
            />
          ))}
          {hasMore && limit < 200 && (
            <Button variant="outline" className="w-full" onClick={() => setLimit((l) => Math.min(l + 50, 200))}>
              {t('showMore')}
            </Button>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Match Result Card ───────────────────────────────────────

interface MatchResultCardProps {
  match: MatchResult;
  isExpanded: boolean;
  onToggle: () => void;
  onStartTrade: (requestedCardIds: number[]) => void;
}

function MatchResultCard({ match, isExpanded, onToggle, onStartTrade }: MatchResultCardProps) {
  const { t } = useTranslation('matchmaking');
  const { number } = useFormat();
  const isMutual = match.isMutual;
  const theyHave = match.theyHaveIWant ?? [];
  const iHave = match.iHaveTheyWant ?? [];
  // Carte dell'altro utente scelte per lo scambio
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const toggle = (cardId: number) => setSelected((prev) => {
    const next = new Set(prev);
    if (next.has(cardId)) next.delete(cardId); else next.add(cardId);
    return next;
  });

  return (
    <div className={`bg-white rounded-2xl border-2 overflow-hidden transition-all shadow-sm ${
      isMutual ? 'border-primary/40' : 'border-border/50'
    }`}>
      {/* Header - always visible */}
      <button onClick={onToggle} className="w-full text-left p-4">
        <div className="flex items-start gap-3">
          {/* Avatar */}
          <div className={`w-12 h-12 rounded-full flex items-center justify-center text-white font-bold text-lg shrink-0 ${
            isMutual ? 'bg-primary' : 'bg-gray-400'
          }`}>
            {match.avatarUrl ? (
              <img src={match.avatarUrl} alt="" className="w-full h-full rounded-full object-cover" />
            ) : (
              match.username.charAt(0).toUpperCase()
            )}
          </div>

          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="font-bold text-sm text-text truncate">{match.username}</span>
              <PlanBadge badge={match.planBadge} />
              {isMutual && (
                <span className="text-[10px] font-bold text-primary bg-primary/10 px-2 py-0.5 rounded-full whitespace-nowrap">
                  <ArrowLeftRight size={10} className="inline mr-0.5" />
                  {t('card.mutualTrade')}
                </span>
              )}
            </div>

            {/* Info row */}
            <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 mt-1 text-[11px] text-text-muted">
              {match.distanceKm != null && (
                <span className="flex items-center gap-0.5">
                  <MapPin size={10} />
                  {match.distanceKm < 1 ? '<1' : Math.round(match.distanceKm)} km
                  {match.city && <span className="hidden sm:inline"> - {match.city}</span>}
                </span>
              )}
              {match.city && (
                <span className="flex items-center gap-0.5 sm:hidden">
                  {match.city}
                </span>
              )}
              {match.reputationScore > 0 && (
                <span className="flex items-center gap-0.5">
                  <Star size={10} className="text-amber-500" />
                  {number(match.reputationScore, { minimumFractionDigits: 1, maximumFractionDigits: 1 })}
                </span>
              )}
              {match.totalTradesCompleted > 0 && (
                <span className="flex items-center gap-0.5">
                  <Trophy size={10} />
                  {t('card.tradesCompleted', { count: match.totalTradesCompleted })}
                </span>
              )}
            </div>

            {/* Card summary pills */}
            <div className="flex flex-wrap gap-1.5 mt-2">
              {theyHave.length > 0 && (
                <span className="text-[10px] font-semibold bg-green-50 text-green-700 border border-green-200 px-2 py-0.5 rounded-full">
                  {t('card.theyHaveCount', { count: theyHave.length })}
                </span>
              )}
              {iHave.length > 0 && (
                <span className="text-[10px] font-semibold bg-blue-50 text-blue-700 border border-blue-200 px-2 py-0.5 rounded-full">
                  {t('card.iHaveCount', { count: iHave.length })}
                </span>
              )}
            </div>
          </div>

          {/* Expand icon */}
          <div className="shrink-0 mt-1">
            {isExpanded ? <ChevronUp size={18} className="text-text-muted" /> : <ChevronDown size={18} className="text-text-muted" />}
          </div>
        </div>
      </button>

      {/* Expanded detail */}
      {isExpanded && (
        <div className="border-t border-border/50 px-4 pb-4">
          {/* They have, I want */}
          {theyHave.length > 0 && (
            <div className="mt-3">
              <p className="text-[11px] font-bold text-green-700">
                {t('card.theyHaveTitle', { n: theyHave.length })}
              </p>
              <p className="text-[11px] text-text-muted mb-2">{t('card.selectHint')}</p>
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-2">
                {theyHave.map((card) => (
                  <MiniCard
                    key={`they-${card.cardId}`}
                    card={card}
                    variant="want"
                    selected={selected.has(card.cardId)}
                    onToggle={() => toggle(card.cardId)}
                  />
                ))}
              </div>
            </div>
          )}

          {/* I have, they want */}
          {iHave.length > 0 && (
            <div className="mt-3">
              <p className="text-[11px] font-bold text-blue-700 mb-2">
                {t('card.iHaveTitle', { n: iHave.length })}
              </p>
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-2">
                {iHave.map((card) => (
                  <MiniCard key={`mine-${card.cardId}`} card={card} variant="have" />
                ))}
              </div>
            </div>
          )}

          {/* Action button */}
          <div className="mt-4">
            <Button
              onClick={() => onStartTrade([...selected])}
              disabled={selected.size === 0}
              className="w-full"
              size="lg"
            >
              <ArrowLeftRight size={16} />
              {selected.size > 0 ? t('card.proposeTradeWith', { count: selected.size }) : t('card.proposeTrade')}
              <ArrowRight size={14} />
            </Button>
            {selected.size === 0 && theyHave.length > 0 && (
              <p className="text-[11px] text-text-muted text-center mt-1.5">{t('card.selectToPropose')}</p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Mini Card Component ─────────────────────────────────────

function MiniCard({ card, variant, selected = false, onToggle }: {
  card: MatchCard;
  variant: 'want' | 'have';
  selected?: boolean;
  onToggle?: () => void;
}) {
  const { t } = useTranslation('matchmaking');
  const { euro } = useFormat();
  const [imgFailed, setImgFailed] = useState(false);
  const borderColor = selected
    ? 'border-primary ring-2 ring-primary/30 bg-primary/5'
    : variant === 'want' ? 'border-green-200 bg-white' : 'border-blue-200 bg-white';
  const priorityBadge = card.wishlistPriority === 1
    ? 'bg-red-50 text-red-600'
    : card.wishlistPriority === 2
    ? 'bg-amber-50 text-amber-600'
    : 'bg-gray-50 text-gray-500';
  const priorityLabel = card.wishlistPriority === 1 ? t('priority.high') : card.wishlistPriority === 2 ? t('priority.medium') : t('priority.low');

  const Wrapper = onToggle ? 'button' : 'div';
  return (
    <Wrapper
      {...(onToggle ? { type: 'button' as const, onClick: onToggle, 'aria-pressed': selected } : {})}
      className={`relative flex gap-2 p-2 rounded-xl border text-left w-full transition-all ${borderColor} ${
        onToggle ? 'hover:border-primary/50 active:scale-[0.98] focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary' : ''
      }`}
    >
      {onToggle && (
        <span
          aria-hidden="true"
          className={`absolute top-1.5 right-1.5 w-5 h-5 rounded-full border-2 flex items-center justify-center ${
            selected ? 'bg-primary border-primary text-white' : 'bg-white border-border'
          }`}
        >
          {selected && <Check size={12} strokeWidth={3} />}
        </span>
      )}
      {card.imageSmall && !imgFailed ? (
        <img src={card.imageSmall} alt="" onError={() => setImgFailed(true)} className="w-10 h-14 rounded-lg object-cover shrink-0" />
      ) : (
        <div className="w-10 h-14 rounded-lg bg-gray-100 flex items-center justify-center shrink-0">
          <span className="text-[8px] text-gray-500 text-center leading-tight px-0.5">{card.name}</span>
        </div>
      )}
      <div className={`min-w-0 flex-1 ${onToggle ? 'pr-5' : ''}`}>
        <p className="text-[11px] font-semibold text-text truncate">{card.name}</p>
        <p className="text-[9px] text-text-muted truncate">{card.setName}</p>
        <p className="text-[9px] text-text-muted">{card.condition}</p>
        {card.priceEur != null && card.priceEur > 0 && (
          <p className="text-[10px] font-bold text-accent">{euro(card.priceEur)}</p>
        )}
        <span className={`text-[8px] font-semibold px-1.5 py-0.5 rounded-full inline-block mt-0.5 ${priorityBadge}`}>
          {priorityLabel}
        </span>
      </div>
    </Wrapper>
  );
}
