import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Loader2, Store as StoreIcon } from 'lucide-react';
import { stores } from '../../api';
import type { Store } from '../../types';
import { apiErrorMessage } from '../../utils/apiError';
import Button from '../ui/Button';
import EmptyState from '../ui/EmptyState';

/** Coda dei negozi da verificare (moderatori). */
export default function StoresModeration() {
  const [items, setItems] = useState<{ ownerUsername: string; createdAt: string; store: Store }[] | null>(null);
  const [notes, setNotes] = useState<Record<number, string>>({});
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    stores.pending().then(({ data }) => setItems(data)).catch((err) => { setItems([]); setError(apiErrorMessage(err, 'Accesso negato.')); });
  }, []);
  useEffect(() => { load(); }, [load]);

  const review = async (id: number, decision: 'verify' | 'reject') => {
    setError(null);
    try {
      await stores.review(id, decision, notes[id]?.trim() || undefined);
      setItems((prev) => (prev ?? []).filter((i) => i.store.id !== id));
    } catch (err) {
      setError(apiErrorMessage(err, 'Operazione non riuscita.'));
    }
  };

  if (items === null) return <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>;

  return (
    <div className="space-y-3">
      {error && <div className="p-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}
      {items.length === 0 ? (
        <EmptyState icon={StoreIcon} title="Nessun negozio da verificare" description="Le nuove richieste compariranno qui." />
      ) : items.map(({ store, ownerUsername, createdAt }) => (
        <div key={store.id} className="p-3 rounded-2xl border border-border bg-white space-y-2">
          <div>
            <Link to={`/stores/${store.id}`} className="text-sm font-semibold hover:underline">{store.name}</Link>
            <p className="text-[11px] text-text-muted">
              {store.address}, {store.city} · inserito da @{ownerUsername} il {new Date(createdAt).toLocaleDateString('it-IT')}
            </p>
          </div>
          {store.description && <p className="text-xs text-text-secondary">{store.description}</p>}
          <p className="text-[11px] text-text-secondary">
            {[store.website, store.instagram && `@${store.instagram}`, store.phone].filter(Boolean).join(' · ') || 'Nessun contatto indicato'}
          </p>
          <input
            value={notes[store.id] ?? ''}
            onChange={(e) => setNotes({ ...notes, [store.id]: e.target.value })}
            placeholder="Motivo (obbligatorio se non approvi, lo vedrà il proprietario)"
            maxLength={500}
            className="w-full px-3 py-2 rounded-xl border border-border text-xs"
          />
          <div className="grid grid-cols-2 gap-1.5">
            <Button size="sm" variant="outline" disabled={!notes[store.id]?.trim()} onClick={() => review(store.id, 'reject')}>Non approvare</Button>
            <Button size="sm" onClick={() => review(store.id, 'verify')}>Verifica</Button>
          </div>
        </div>
      ))}
    </div>
  );
}
