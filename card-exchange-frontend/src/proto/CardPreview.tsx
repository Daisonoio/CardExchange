import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, ArrowLeftRight, ChevronRight, Heart, Info, Loader2, MessageCircle, Sparkles, Star } from 'lucide-react';
import { CONDITION_LABELS, FINISH_LABELS, finishValue, languageLabel } from '../types';
import { useFormat } from '../i18n/format';
import { apiErrorMessage } from '../utils/apiError';
import { explore, type ExploreCardDetail } from './api';
import { DistanceBadge, PriceLine } from './parts';

const CONDITION_BY_NAME: Record<string, keyof typeof CONDITION_LABELS> = {
  Mint: 1, NearMint: 2, Excellent: 3, Good: 4, LightlyPlayed: 5, ModeratelyPlayed: 6, HeavilyPlayed: 7, Damaged: 8,
};

/** Anteprima: scheda carta con immagine grande, proprietario e due azioni chiare. */
export default function CardPreview() {
  const { t } = useTranslation('proto');
  const { euro, date } = useFormat();
  const { id } = useParams();
  const navigate = useNavigate();
  const [card, setCard] = useState<ExploreCardDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    explore.card(Number(id)).then(({ data }) => setCard(data)).catch((err) => setError(apiErrorMessage(err, t('errors.card'))));
  }, [id, t]);

  if (error) return <div className="p-6 text-center"><p style={{ color: 'var(--cx-danger)' }}>{error}</p><button onClick={() => navigate(-1)} className="cx-btn-outline px-5 py-2 mt-4">{t('back')}</button></div>;
  if (!card) return <div className="flex justify-center py-24"><Loader2 className="animate-spin cx-accent-text" /></div>;

  const condition = CONDITION_BY_NAME[card.condition];
  const finish = finishValue(card.finish);
  const tags = [
    condition ? CONDITION_LABELS[condition] : card.condition,
    finish && finish !== 1 ? FINISH_LABELS[finish] : null,
    card.language ? languageLabel(card.language) : null,
    card.quantity > 1 ? `×${card.quantity}` : null,
  ].filter(Boolean) as string[];

  return (
    <div>
      {/* Immagine */}
      <div className="relative px-4 pt-4">
        <div className="flex justify-between absolute inset-x-4 top-4 z-10 px-3 pt-3">
          <button onClick={() => navigate(-1)} aria-label={t('back')} className="w-10 h-10 rounded-full flex items-center justify-center" style={{ background: 'rgba(13,11,30,0.55)', color: '#fff' }}><ArrowLeft size={20} /></button>
          {card.isWanted && <span className="w-10 h-10 rounded-full flex items-center justify-center" style={{ background: 'rgba(13,11,30,0.55)' }} title={t('wantedHint')}><Heart size={18} fill="#ff6b7a" stroke="#ff6b7a" /></span>}
        </div>
        <div className="rounded-[28px] p-6 flex justify-center" style={{ background: 'linear-gradient(160deg, var(--cx-surface-2), var(--cx-surface))' }}>
          {card.imageUrl
            ? <img src={card.imageUrl} alt={card.name} className="w-[68%] max-w-[260px] aspect-[63/88] object-cover rounded-[14px]" style={{ boxShadow: '0 24px 50px rgba(0,0,0,0.45)' }} />
            : <div className="w-[68%] aspect-[63/88] rounded-[14px] flex items-center justify-center cx-muted" style={{ background: 'var(--cx-surface-2)' }}>{card.name}</div>}
        </div>
      </div>

      <div className="cx-sheet -mt-5 relative px-5 pt-6 pb-6 space-y-5">
        <div>
          <h1 className="text-2xl font-extrabold leading-tight">{card.name}</h1>
          <p className="text-sm cx-muted mt-1">{[card.setName, card.number, card.rarity].filter(Boolean).join(' · ')}</p>
        </div>

        <div className="flex items-end justify-between gap-3">
          <PriceLine price={card.price} kind={card.priceKind} size="lg" />
          <DistanceBadge km={card.approxKm} className="text-xs px-2.5 py-1" />
        </div>
        {card.priceKind === 'asking' && card.marketValue != null && (
          <p className="text-xs cx-muted -mt-3">{t('card.marketRef', { value: euro(card.marketValue) })}</p>
        )}

        <div className="flex flex-wrap gap-2">
          {tags.map((tag) => <span key={tag} className="cx-chip px-3 py-1.5 text-xs font-semibold">{tag}</span>)}
        </div>

        {/* Proprietario */}
        <Link to={`/collection/${card.owner.id}`} className="cx-card flex items-center gap-3 p-3" style={{ background: 'var(--cx-surface-2)' }}>
          <span className="w-12 h-12 rounded-full flex items-center justify-center text-lg font-extrabold shrink-0 overflow-hidden" style={{ background: 'var(--cx-accent)', color: 'var(--cx-on-accent)' }}>
            {card.owner.avatarUrl ? <img src={card.owner.avatarUrl} alt="" className="w-full h-full object-cover" /> : card.owner.username[0]?.toUpperCase()}
          </span>
          <span className="flex-1 min-w-0">
            <span className="flex items-center gap-1.5 font-bold truncate">
              {card.owner.username}
              {card.owner.planBadge && <span className="cx-badge text-[10px] px-1.5 py-0.5">{card.owner.planBadge === 'Store' ? t('badge.store') : 'Premium'}</span>}
            </span>
            <span className="flex items-center gap-1 text-xs cx-muted mt-0.5">
              <Star size={12} fill="#f5b800" stroke="#f5b800" />
              {card.owner.totalReviewsReceived > 0
                ? t('card.rating', { score: card.owner.reputationScore.toFixed(1), count: card.owner.totalReviewsReceived })
                : t('card.noReviews')}
              {' · '}{t('card.memberSince', { date: date(card.owner.memberSince, { month: 'short', year: 'numeric' }) })}
            </span>
          </span>
          <ChevronRight size={18} className="cx-muted" />
        </Link>

        {card.ownerWantsFromMe > 0 && (
          <div className="rounded-2xl p-3 flex gap-2.5 text-sm" style={{ background: 'var(--cx-accent-soft)' }}>
            <Sparkles size={18} className="cx-accent-text shrink-0 mt-0.5" />
            <span>{t('card.ownerWants', { user: card.owner.username, count: card.ownerWantsFromMe })}</span>
          </div>
        )}

        {card.isOwn ? (
          <p className="text-sm cx-muted text-center">{t('card.own')}</p>
        ) : (
          <div className="space-y-3">
            <button onClick={() => navigate(`/trades/new/${card.owner.id}`)} className="cx-btn w-full py-3.5 flex items-center justify-center gap-2 text-[15px]">
              <ArrowLeftRight size={18} /> {t('card.proposeTrade')}
            </button>
            <button onClick={() => navigate(`/chat?userId=${card.owner.id}`)} className="cx-btn-outline w-full py-3 flex items-center justify-center gap-2 text-[15px]">
              <MessageCircle size={18} /> {t('card.message', { user: card.owner.username })}
            </button>
          </div>
        )}

        {card.notes && (
          <div>
            <p className="font-bold mb-1">{t('card.notes')}</p>
            <p className="text-sm cx-text-2 whitespace-pre-line">{card.notes}</p>
          </div>
        )}

        <p className="text-[11px] cx-muted flex gap-1.5"><Info size={13} className="shrink-0 mt-0.5" />{t('card.offlineNote')}</p>
      </div>
    </div>
  );
}
