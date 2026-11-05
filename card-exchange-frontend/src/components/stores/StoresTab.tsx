import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ChevronRight, Loader2, Plus, ShieldCheck, Sparkles, Store as StoreIcon } from 'lucide-react';
import { stores } from '../../api';
import type { Store } from '../../types';
import { useGame } from '../../context/GameContext';
import EmptyState from '../ui/EmptyState';
import EmptyStateLink from '../ui/EmptyStateLink';
import StoresMap from './StoresMap';

/** Negozi verificati vicini (in base alla posizione del profilo) e negozi gestiti dall'utente. */
export default function StoresTab() {
  const { t } = useTranslation('stores');
  const { selectedGameId } = useGame();
  const [items, setItems] = useState<Store[] | null>(null);
  const [mine, setMine] = useState<Store[]>([]);
  const [meetupOnly, setMeetupOnly] = useState(false);

  useEffect(() => {
    let cancelled = false;
    stores.list({ radiusKm: 100, gameId: selectedGameId ?? undefined, meetupOnly })
      .then(({ data }) => { if (!cancelled) setItems(data); })
      .catch(() => { if (!cancelled) setItems([]); });
    return () => { cancelled = true; };
  }, [selectedGameId, meetupOnly]);

  useEffect(() => {
    stores.mine().then(({ data }) => setMine(data)).catch(() => setMine([]));
  }, []);

  return (
    <div className="space-y-4">
      {mine.length > 0 && (
        <section className="bg-white rounded-2xl border border-border p-3 space-y-1.5">
          <h2 className="text-sm font-semibold">{t('tab.mine')}</h2>
          {mine.map((s) => (
            <Link key={s.id} to={`/stores/${s.id}`} className="flex items-center justify-between p-2 rounded-xl hover:bg-surface-dark text-sm">
              <span className="truncate">{s.name}</span>
              <span className={`text-[10px] px-2 py-0.5 rounded-full shrink-0 ${s.status === 'Verified' ? 'bg-green-50 text-green-700' : s.status === 'Pending' ? 'bg-amber-50 text-amber-700' : 'bg-red-50 text-red-700'}`}>
                {s.status === 'Verified' ? t('tab.status.verified') : s.status === 'Pending' ? t('tab.status.pending') : s.status === 'Rejected' ? t('tab.status.rejected') : t('tab.status.suspended')}
              </span>
            </Link>
          ))}
        </section>
      )}

      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" className="w-4 h-4 accent-primary" checked={meetupOnly} onChange={(e) => setMeetupOnly(e.target.checked)} />
        {t('tab.meetupOnly')}
      </label>

      {items === null ? (
        <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>
      ) : items.length === 0 ? (
        <EmptyState
          icon={StoreIcon}
          title={t('tab.emptyTitle')}
          description={t('tab.emptyDescription')}
          action={<EmptyStateLink to="/stores/new" label={t('tab.emptyAction')} />}
        />
      ) : (
        <>
          <StoresMap items={items} />
          <ul className="space-y-2">
            {items.map((s) => (
              <li key={s.id}>
                <Link to={`/stores/${s.id}`} className={`flex items-center gap-3 p-3 rounded-2xl bg-white border hover:shadow-sm ${s.isFeatured ? 'border-secondary ring-1 ring-secondary/40' : 'border-border'}`}>
                  <span className="w-10 h-10 rounded-xl bg-primary/10 text-primary flex items-center justify-center shrink-0"><StoreIcon size={18} /></span>
                  <span className="flex-1 min-w-0">
                    <span className="block text-sm font-semibold truncate">{s.name}</span>
                    <span className="block text-xs text-text-muted truncate">
                      {s.distanceKm != null ? `${s.distanceKm} km · ` : ''}{s.address}, {s.city}
                    </span>
                    <span className="flex gap-2 mt-0.5 text-[10px]">
                      {s.isFeatured && <span className="inline-flex items-center gap-0.5 font-semibold text-amber-700"><Sparkles size={11} /> {t('tab.featured')}</span>}
                      {s.isSafeMeetupSpot && <span className="inline-flex items-center gap-0.5 text-green-700"><ShieldCheck size={11} /> {t('tab.meetupSpot')}</span>}
                      {s.upcomingEventsCount > 0 && <span className="text-primary">{t('tab.events', { count: s.upcomingEventsCount })}</span>}
                    </span>
                  </span>
                  <ChevronRight size={16} className="text-text-muted shrink-0" />
                </Link>
              </li>
            ))}
          </ul>
        </>
      )}

      <Link to="/stores/new" className="flex items-center justify-center gap-2 p-3 rounded-2xl border-2 border-dashed border-border text-sm font-semibold text-primary hover:bg-surface-dark">
        <Plus size={16} /> {t('tab.addStore')}
      </Link>
    </div>
  );
}
