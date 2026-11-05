import { useEffect, useState } from 'react';
import { Gift, Share2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { referrals } from '../../api';
import { shareLink } from '../../utils/referral';

/** Link personale di invito e amici che si sono iscritti. */
export default function InviteCard() {
  const { t } = useTranslation('users');
  const [data, setData] = useState<Awaited<ReturnType<typeof referrals.mine>>['data'] | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  useEffect(() => {
    referrals.mine().then(({ data }) => setData(data)).catch(() => setData(null));
  }, []);

  if (!data) return null;
  const reward = data.reward;
  const capped = !!reward && reward.monthsThisYear >= reward.maxMonthsPerYear;

  const share = async () => {
    const result = await shareLink(data.link, 'CardExchange',
      t('invite.shareText'));
    setFeedback(result === 'copied' ? t('invite.copied') : result === 'shared' ? null : t('invite.copyManually'));
  };

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-border/50 p-4 mb-4 space-y-2">
      <div className="flex items-center gap-3">
        <span className="w-9 h-9 rounded-full border-2 border-border flex items-center justify-center shrink-0" style={{ color: '#1a3461' }}>
          <Gift size={16} />
        </span>
        <div className="flex-1">
          <p className="text-sm font-medium text-text">{t('invite.title')}</p>
          <p className="text-xs text-text-muted">{t('invite.subtitle')}</p>
        </div>
      </div>
      <div className="flex items-center gap-2">
        <input readOnly value={data.link} aria-label={t('invite.linkLabel')} onFocus={(e) => e.target.select()}
          className="flex-1 min-w-0 px-3 py-2 rounded-xl border border-border text-xs bg-surface-dark" />
        <button type="button" onClick={share} className="inline-flex items-center gap-1 px-3 py-2 rounded-xl bg-primary text-white text-xs font-semibold">
          <Share2 size={14} /> {t('invite.share')}
        </button>
      </div>
      {feedback && <p className="text-xs text-text-secondary">{feedback}</p>}
      {reward && (
        <div className="pt-1">
          {capped ? (
            <p className="text-xs font-medium text-green-700">{t('invite.capReached', { max: reward.maxMonthsPerYear })}</p>
          ) : (
            <>
              <div className="flex items-center justify-between text-xs">
                <span className="text-text-secondary">{t('invite.progress', { done: reward.towardsNext, total: reward.perReward })}</span>
                <span className="flex gap-1" aria-hidden>
                  {Array.from({ length: reward.perReward }, (_, i) => (
                    <span key={i} className={`w-2.5 h-2.5 rounded-full ${i < reward.towardsNext ? 'bg-primary' : 'bg-border'}`} />
                  ))}
                </span>
              </div>
              <div className="mt-1 h-1.5 rounded-full bg-surface-dark overflow-hidden" role="progressbar" aria-valuemin={0} aria-valuemax={reward.perReward} aria-valuenow={reward.towardsNext}>
                <div className="h-full bg-primary rounded-full transition-all" style={{ width: `${(reward.towardsNext / reward.perReward) * 100}%` }} />
              </div>
            </>
          )}
          {reward.monthsEarned > 0 && !capped && <p className="mt-1 text-xs font-medium text-green-700">{t('invite.rewardsEarned', { count: reward.monthsEarned })}</p>}
        </div>
      )}
      {data.invitedCount === 0 ? (
        <p className="text-xs text-text-secondary">{t('invite.noneYet')}</p>
      ) : (
        <div>
          <p className="text-xs text-text-secondary">{t('invite.joined', { count: data.invitedCount })}</p>
          <ul className="mt-1 flex flex-wrap gap-1.5">
            {data.invited.slice(0, 12).map((i) => (
              <li key={i.username} className={`px-2 py-0.5 rounded-full text-[11px] ${i.active ? 'bg-green-50 text-green-700' : 'bg-surface-dark text-text-secondary'}`}>
                @{i.username} · {i.active ? t('invite.activeBadge') : t('invite.pendingBadge')}
              </li>
            ))}
          </ul>
        </div>
      )}
      <p className="text-[11px] text-text-muted">{t('invite.activeRule')}</p>
    </div>
  );
}
