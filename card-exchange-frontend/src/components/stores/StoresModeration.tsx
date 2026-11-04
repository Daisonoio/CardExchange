import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Loader2, Store as StoreIcon } from 'lucide-react';
import { stores } from '../../api';
import type { Store } from '../../types';
import { apiErrorMessage } from '../../utils/apiError';
import Button from '../ui/Button';
import EmptyState from '../ui/EmptyState';
import { useFormat } from '../../i18n/format';

/** Coda dei negozi da verificare (moderatori). */
export default function StoresModeration() {
  const { t } = useTranslation('stores');
  const { date } = useFormat();
  const [items, setItems] = useState<{ ownerUsername: string; createdAt: string; store: Store }[] | null>(null);
  const [notes, setNotes] = useState<Record<number, string>>({});
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    stores.pending().then(({ data }) => setItems(data)).catch((err) => { setItems([]); setError(apiErrorMessage(err, t('moderation.accessDenied'))); });
  }, [t]);
  useEffect(() => { load(); }, [load]);

  const review = async (id: number, decision: 'verify' | 'reject') => {
    setError(null);
    try {
      await stores.review(id, decision, notes[id]?.trim() || undefined);
      setItems((prev) => (prev ?? []).filter((i) => i.store.id !== id));
    } catch (err) {
      setError(apiErrorMessage(err, t('moderation.actionError')));
    }
  };

  if (items === null) return <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>;

  return (
    <div className="space-y-3">
      {error && <div className="p-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}
      {items.length === 0 ? (
        <EmptyState icon={StoreIcon} title={t('moderation.emptyTitle')} description={t('moderation.emptyDescription')} />
      ) : items.map(({ store, ownerUsername, createdAt }) => (
        <div key={store.id} className="p-3 rounded-2xl border border-border bg-white space-y-2">
          <div>
            <Link to={`/stores/${store.id}`} className="text-sm font-semibold hover:underline">{store.name}</Link>
            <p className="text-[11px] text-text-muted">
              {store.address}, {store.city} · {t('moderation.submittedBy', { username: ownerUsername, date: date(createdAt, { day: 'numeric', month: 'numeric', year: 'numeric' }) })}
            </p>
          </div>
          {store.description && <p className="text-xs text-text-secondary">{store.description}</p>}
          <p className="text-[11px] text-text-secondary">
            {[store.website, store.instagram && `@${store.instagram}`, store.phone].filter(Boolean).join(' · ') || t('moderation.noContacts')}
          </p>
          <input
            value={notes[store.id] ?? ''}
            onChange={(e) => setNotes({ ...notes, [store.id]: e.target.value })}
            placeholder={t('moderation.notePlaceholder')}
            maxLength={500}
            className="w-full px-3 py-2 rounded-xl border border-border text-xs"
          />
          <div className="grid grid-cols-2 gap-1.5">
            <Button size="sm" variant="outline" disabled={!notes[store.id]?.trim()} onClick={() => review(store.id, 'reject')}>{t('moderation.reject')}</Button>
            <Button size="sm" onClick={() => review(store.id, 'verify')}>{t('moderation.verify')}</Button>
          </div>
        </div>
      ))}
    </div>
  );
}
