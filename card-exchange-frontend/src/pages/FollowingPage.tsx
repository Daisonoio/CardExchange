import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ChevronRight, Loader2, Radio, Store as StoreIcon, UserPlus, Users } from 'lucide-react';
import { follows, type FollowingList } from '../api';
import EmptyState from '../components/ui/EmptyState';
import LiveStrip from '../components/social/LiveStrip';

/** Profili seguiti (collezionisti e negozi) con l'eventuale "Sono qui" in corso. */
export default function FollowingPage() {
  const { t } = useTranslation('live');
  const [data, setData] = useState<FollowingList | null>(null);

  useEffect(() => {
    follows.list().then(({ data }) => setData(data)).catch(() => setData({ users: [], stores: [], followers: 0 }));
  }, []);

  if (!data) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  const empty = data.users.length === 0 && data.stores.length === 0;

  const liveBadge = (live?: { id: number; placeName: string } | null) => live && (
    <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-red-600">
      <span className="w-1.5 h-1.5 rounded-full bg-red-500" aria-hidden="true" /> {t('following.liveAt', { place: live.placeName })}
    </span>
  );

  const row = 'flex items-center gap-3 bg-white rounded-2xl p-3 border border-border/60 hover:border-primary/40 transition-colors';

  return (
    <div className="pb-24 max-w-xl space-y-4">
      <div>
        <h1 className="text-xl font-bold text-text">{t('following.title')}</h1>
        <p className="text-sm text-text-secondary flex items-center gap-1"><Users size={14} /> {t('following.yourFollowers', { count: data.followers })}</p>
      </div>

      <LiveStrip />

      {empty ? (
        <EmptyState icon={UserPlus} title={t('following.empty')} description={t('following.emptyDescription')} />
      ) : (
        <>
          {data.users.length > 0 && (
            <section>
              <h2 className="text-xs font-semibold uppercase tracking-wide text-text-muted mb-2">{t('following.users')}</h2>
              <div className="space-y-2">
                {data.users.map((u) => (
                  <Link key={u.id} to={u.live ? `/live/${u.live.id}` : `/collection/${u.id}`} className={row}>
                    <div className="w-10 h-10 rounded-full bg-primary/10 text-primary font-bold flex items-center justify-center shrink-0">
                      {u.avatarUrl ? <img src={u.avatarUrl} alt="" className="w-10 h-10 rounded-full object-cover" /> : u.username[0].toUpperCase()}
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-semibold text-text truncate">@{u.username}</p>
                      {liveBadge(u.live) || <p className="text-xs text-text-muted truncate">{u.city}</p>}
                    </div>
                    {u.live ? <Radio size={16} className="text-red-500" aria-hidden="true" /> : <ChevronRight size={16} className="text-text-muted" aria-hidden="true" />}
                  </Link>
                ))}
              </div>
            </section>
          )}
          {data.stores.length > 0 && (
            <section>
              <h2 className="text-xs font-semibold uppercase tracking-wide text-text-muted mb-2">{t('following.stores')}</h2>
              <div className="space-y-2">
                {data.stores.map((s) => (
                  <Link key={s.id} to={s.live ? `/live/${s.live.id}` : `/stores/${s.id}`} className={row}>
                    <div className="w-10 h-10 rounded-full bg-primary/10 text-primary flex items-center justify-center shrink-0"><StoreIcon size={18} /></div>
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-semibold text-text truncate">{s.name}</p>
                      {liveBadge(s.live) || <p className="text-xs text-text-muted truncate">{s.city}</p>}
                    </div>
                    {s.live ? <Radio size={16} className="text-red-500" aria-hidden="true" /> : <ChevronRight size={16} className="text-text-muted" aria-hidden="true" />}
                  </Link>
                ))}
              </div>
            </section>
          )}
        </>
      )}
    </div>
  );
}
