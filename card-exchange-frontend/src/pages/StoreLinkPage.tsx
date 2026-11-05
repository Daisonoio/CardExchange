import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Loader2, MapPin, Store as StoreIcon } from 'lucide-react';
import { follows, stores } from '../api';
import { useAuth } from '../context/AuthContext';
import { saveStoreSlug } from '../utils/storeAttribution';
import AuthShell from '../components/layout/AuthShell';

/**
 * Pagina del QR del negozio (/s/:slug). Chi non è registrato vede l'invito a iscriversi: la registrazione
 * lo lega al negozio e glielo fa seguire. Chi ha già un account segue il negozio e ne apre la scheda.
 */
export default function StoreLinkPage() {
  const { t } = useTranslation('qr');
  const { slug = '' } = useParams<{ slug: string }>();
  const { isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const [store, setStore] = useState<{ id: number; name: string; city: string } | null | undefined>(undefined);

  useEffect(() => {
    stores.bySlug(slug).then(({ data }) => {
      setStore(data);
      saveStoreSlug(data.slug);
    }).catch(() => setStore(null));
  }, [slug]);

  useEffect(() => {
    if (isLoading || !isAuthenticated || !store) return;
    follows.followStore(store.id).catch(() => undefined).finally(() => navigate(`/stores/${store.id}`, { replace: true }));
  }, [isLoading, isAuthenticated, store, navigate]);

  if (store === undefined || (isAuthenticated && store)) {
    return <div className="min-h-screen flex items-center justify-center"><Loader2 className="animate-spin text-primary" /></div>;
  }

  return (
    <AuthShell subtitle={store ? t('link.subtitle') : t('link.notFound')}>
      {store ? (
        <div className="space-y-4 text-center">
          <div className="w-14 h-14 mx-auto rounded-2xl bg-primary/10 text-primary flex items-center justify-center"><StoreIcon size={26} /></div>
          <div>
            <p className="text-lg font-bold text-text">{store.name}</p>
            <p className="text-sm text-text-secondary flex items-center justify-center gap-1"><MapPin size={14} /> {store.city}</p>
          </div>
          <p className="text-sm text-text-secondary">{t('link.pitch')}</p>
          <Link to="/register" className="block w-full py-3 rounded-xl bg-primary text-white font-semibold hover:bg-primary-light">{t('link.register')}</Link>
          <Link to="/login" className="block text-sm font-semibold text-primary hover:underline">{t('link.login')}</Link>
        </div>
      ) : (
        <div className="space-y-3 text-center">
          <p className="text-sm text-text-secondary">{t('link.notFoundDescription')}</p>
          <Link to="/register" className="block w-full py-3 rounded-xl bg-primary text-white font-semibold">{t('link.register')}</Link>
        </div>
      )}
    </AuthShell>
  );
}
