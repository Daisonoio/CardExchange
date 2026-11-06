import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Coins, Loader2 } from 'lucide-react';
import { purchases } from '../api';
import { useFormat } from '../i18n/format';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';

type Mine = Awaited<ReturnType<typeof purchases.mine>>['data'];
type Products = Awaited<ReturnType<typeof purchases.products>>['data'];

/** Saldo dei crediti promozione e storico degli acquisti una tantum (pagina di ritorno da Stripe). */
export default function PurchasesPage() {
  const { t } = useTranslation('purchases');
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { euro, dateTime } = useFormat();
  const [mine, setMine] = useState<Mine | null>(null);
  const [products, setProducts] = useState<Products | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const checkout = params.get('checkout');

  const load = useCallback(() => {
    purchases.mine().then(({ data }) => setMine(data)).catch(() => setMine({ credits: 0, purchases: [] }));
    purchases.products().then(({ data }) => setProducts(data)).catch(() => setProducts(null));
  }, []);
  useEffect(() => { load(); }, [load]);

  // Ritorno da Stripe: l'attivazione arriva dal webhook, si ricarica qualche volta
  useEffect(() => {
    if (checkout !== 'success') return;
    const timers = [2000, 5000, 10000].map((ms) => setTimeout(load, ms));
    return () => timers.forEach(clearTimeout);
  }, [checkout, load]);

  const buy = async (code: string) => {
    setBusy(code);
    setError(null);
    try {
      const { data } = await purchases.checkout(code);
      window.location.assign(data.url);
    } catch (err) {
      setError(apiErrorMessage(err, t('notYet')));
      setBusy(null);
    }
  };

  return (
    <div className="max-w-lg mx-auto pb-24 space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0"><ArrowLeft size={18} /></button>
        <h1 className="text-xl font-bold text-text">{t('title')}</h1>
      </div>
      {checkout === 'success' && <p role="status" className="p-3 rounded-xl bg-green-50 text-green-800 text-sm">{t('success')}</p>}
      {checkout === 'cancelled' && <p role="status" className="p-3 rounded-xl bg-amber-50 text-amber-800 text-sm">{t('cancelled')}</p>}
      {error && <p role="alert" className="p-3 rounded-xl bg-red-50 text-red-700 text-sm">{error}</p>}
      {mine === null ? (
        <div className="flex justify-center py-12"><Loader2 className="animate-spin text-primary" /></div>
      ) : (
        <>
          <section className="p-4 rounded-2xl bg-white border border-border">
            <p className="flex items-center gap-2 text-lg font-bold text-text"><Coins size={20} className="text-amber-600" /> {t('credits', { count: mine.credits })}</p>
            <p className="text-xs text-text-secondary mt-1">{t('creditsHint')}</p>
          </section>
          {products && (products.enabled ? (
            <section className="grid grid-cols-2 gap-2">
              {products.products.map((p) => (
                <div key={p.code} className="p-3 rounded-2xl bg-white border border-border space-y-2">
                  <p className="text-sm font-semibold text-text">{p.name}</p>
                  <p className="text-lg font-bold text-text">{euro(p.priceEur)}</p>
                  <Button size="sm" className="w-full" isLoading={busy === p.code} disabled={!!busy} onClick={() => buy(p.code)}>{t('buy')}</Button>
                </div>
              ))}
            </section>
          ) : <p className="text-xs text-text-muted">{t('notYet')}</p>)}
          <section className="bg-white rounded-2xl border border-border p-3">
            <h2 className="text-sm font-semibold text-text mb-1">{t('history')}</h2>
            {mine.purchases.length === 0 ? <p className="text-sm text-text-muted">{t('none')}</p> : (
              <ul className="divide-y divide-border/60">
                {mine.purchases.map((p) => (
                  <li key={p.id} className="py-2 flex items-center justify-between gap-2 text-sm">
                    <span className="min-w-0">
                      <span className="block text-text">{p.productCode} · {euro(p.amountEur)}</span>
                      <span className="block text-xs text-text-muted">{dateTime(p.createdAt, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })}</span>
                    </span>
                    <span className="text-xs text-text-secondary">{t(`status.${p.status}`)}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  );
}
