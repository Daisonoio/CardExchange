import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { safety } from '../../api';
import BottomSheet from '../ui/BottomSheet';

/** Elenco degli utenti bloccati, con sblocco. */
export default function BlockedUsersSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [items, setItems] = useState<{ userId: number; username: string; blockedAt: string }[] | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    safety.blocked()
      .then(({ data }) => { if (!cancelled) setItems(data); })
      .catch(() => { if (!cancelled) setItems([]); });
    // Alla chiusura si azzera, così alla riapertura si ricarica
    return () => { cancelled = true; setItems(null); };
  }, [open]);

  const loading = open && items === null;

  const unblock = async (userId: number) => {
    await safety.unblock(userId);
    setItems((prev) => (prev ?? []).filter((i) => i.userId !== userId));
  };

  return (
    <BottomSheet open={open} onClose={onClose} title="Utenti bloccati">
      {loading ? (
        <div className="flex justify-center py-8"><Loader2 className="animate-spin text-primary" /></div>
      ) : !items || items.length === 0 ? (
        <p className="text-sm text-text-muted text-center py-8">Non hai bloccato nessuno.</p>
      ) : (
        <ul className="space-y-1.5">
          {items.map((i) => (
            <li key={i.userId} className="flex items-center justify-between p-3 rounded-xl border border-border">
              <div>
                <p className="text-sm font-semibold">@{i.username}</p>
                <p className="text-[10px] text-text-muted">dal {new Date(i.blockedAt).toLocaleDateString('it-IT')}</p>
              </div>
              <button type="button" onClick={() => unblock(i.userId)} className="text-xs font-semibold text-primary hover:underline">Sblocca</button>
            </li>
          ))}
        </ul>
      )}
    </BottomSheet>
  );
}
