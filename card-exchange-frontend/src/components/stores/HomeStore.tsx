import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Home, Loader2, Store as StoreIcon } from 'lucide-react';
import { stores, type HomeStore } from '../../api';

/** Negozio di casa dell'utente: caricato una volta, con le azioni per sceglierlo o toglierlo. */
function useHomeStore() {
  const [home, setHome] = useState<HomeStore | null | undefined>(undefined);
  const reload = useCallback(() => stores.home().then(({ data }) => setHome(data.store ?? null)).catch(() => setHome(null)), []);
  useEffect(() => { reload(); }, [reload]);
  return { home, reload };
}

/** Nella scheda di un negozio (non del proprietario): "Rendilo il mio negozio di casa" oppure lo stato attuale. */
export function HomeStoreToggle({ storeId }: { storeId: number }) {
  const { t } = useTranslation('stores');
  const { home, reload } = useHomeStore();
  const [busy, setBusy] = useState(false);
  if (home === undefined) return null;
  const isHome = home?.id === storeId;

  const run = (action: () => Promise<unknown>) => {
    setBusy(true);
    action().then(reload).catch(() => undefined).finally(() => setBusy(false));
  };

  return (
    <div className={`flex flex-wrap items-center gap-3 p-3 rounded-xl border ${isHome ? 'bg-primary/10 border-primary/40' : 'bg-surface-card border-border'}`}>
      <span className={`w-9 h-9 rounded-full flex items-center justify-center shrink-0 ${isHome ? 'bg-primary text-on-primary' : 'bg-surface-raised text-primary'}`}>
        <Home size={17} />
      </span>
      <span className="flex-1 min-w-0">
        <strong className="block text-sm text-text">{isHome ? t('home.isHome') : t('home.title')}</strong>
        <span className="block text-xs text-text-secondary">{t('home.hint')}</span>
      </span>
      {busy ? <Loader2 size={18} className="animate-spin text-primary shrink-0" /> : isHome ? (
        <button type="button" onClick={() => run(() => stores.clearHome())} className="text-xs font-semibold text-text-secondary hover:text-text shrink-0">
          {t('home.remove')}
        </button>
      ) : (
        // Il bottone va sotto il testo: su telefono non stringe la descrizione
        <button type="button" onClick={() => run(() => stores.setHome(storeId))}
          className="w-full py-2 rounded-xl bg-primary text-on-primary text-sm font-semibold hover:bg-primary-light">
          {home ? t('home.change') : t('home.set')}
        </button>
      )}
    </div>
  );
}

/** Nel profilo: il negozio di casa con il collegamento alla sua scheda, oppure l'invito a sceglierne uno. */
export function HomeStoreCard() {
  const { t } = useTranslation('stores');
  const { home } = useHomeStore();
  if (home === undefined) return null;

  return (
    <div className="bg-surface-card rounded-2xl border border-border/50 p-4 mb-4">
      <div className="flex items-center gap-3">
        <span className="w-9 h-9 rounded-full bg-primary/15 text-primary flex items-center justify-center shrink-0"><Home size={17} /></span>
        <span className="flex-1 min-w-0">
          <span className="block text-xs text-text-muted">{t('home.title')}</span>
          {home ? (
            <Link to={`/stores/${home.id}`} className="block text-sm font-semibold text-text truncate hover:text-primary">
              <StoreIcon size={13} className="inline -mt-0.5 mr-1 text-primary" />{home.name} · {home.city}
            </Link>
          ) : (
            <span className="block text-xs text-text-secondary">{t('home.noneHint')}</span>
          )}
        </span>
        <Link to="/local" className="text-xs font-semibold text-primary hover:underline shrink-0">
          {home ? t('home.change') : t('home.choose')}
        </Link>
      </div>
    </div>
  );
}
