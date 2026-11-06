import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Camera, CheckCircle2, Gift, Loader2, Stamp } from 'lucide-react';
import { loyalty, type LoyaltyInfo, type LoyaltyScanResult } from '../api';
import { apiErrorMessage } from '../utils/apiError';
import Gate from '../components/ui/Gate';
import Button from '../components/ui/Button';
import QrReader from '../components/qr/QrReader';
import StampRow from '../components/loyalty/StampRow';

type Mode = 'stamp' | 'redeem';

/** Per il negozio: tessera fedeltà (timbri e premio), scansione del QR del cliente, dati aggregati. */
export default function StoreLoyaltyPage() {
  const { t } = useTranslation('loyalty');
  const { id } = useParams<{ id: string }>();
  const storeId = Number(id);
  const [info, setInfo] = useState<LoyaltyInfo | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [form, setForm] = useState({ stampsRequired: 5, rewardDescription: '', isActive: true });
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState<string | null>(null);
  const [mode, setMode] = useState<Mode>('stamp');
  const [scanning, setScanning] = useState(false);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<(LoyaltyScanResult & { mode: Mode }) | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    loyalty.forStore(storeId).then(({ data }) => {
      if (data.hasPlan === undefined) { setForbidden(true); return; }
      setInfo(data);
      setForm({ stampsRequired: data.stampsRequired ?? 5, rewardDescription: data.rewardDescription ?? '', isActive: data.isActive ?? true });
    }).catch(() => setForbidden(true));
  }, [storeId]);
  useEffect(() => { load(); }, [load]);

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true); setError(null); setSaved(null);
    try {
      const { data } = await loyalty.configure(storeId, form);
      setSaved((data as { message?: string }).message ?? t('store.saved'));
      load();
    } catch (err) {
      setError(apiErrorMessage(err, t('store.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  const submit = useCallback(async (token: string) => {
    const clean = token.trim();
    if (clean.length < 8 || busy) return;
    setBusy(true); setError(null); setResult(null);
    try {
      const { data } = mode === 'stamp' ? await loyalty.stamp(storeId, clean) : await loyalty.redeem(storeId, clean);
      setResult({ ...data, mode });
      setCode('');
      if (navigator.vibrate) navigator.vibrate(80);
      load();
    } catch (err) {
      setError(apiErrorMessage(err, t('scan.failed')));
    } finally {
      setBusy(false);
    }
  }, [busy, mode, storeId, load, t]);

  if (forbidden) {
    return <p className="text-sm text-text-muted">{t('results.forbidden', { ns: 'stores' })}</p>;
  }
  if (!info) return <div className="flex justify-center py-12"><Loader2 className="animate-spin text-primary" /></div>;

  const stats = info.stats;
  const tab = (m: Mode, label: string, Icon: typeof Stamp) => (
    <button type="button" onClick={() => { setMode(m); setResult(null); setError(null); }}
      aria-pressed={mode === m}
      className={`flex-1 flex items-center justify-center gap-1.5 py-2 rounded-xl text-sm font-semibold ${mode === m ? 'bg-primary text-white' : 'bg-white border border-border text-text'}`}>
      <Icon size={15} /> {label}
    </button>
  );

  return (
    <div className="pb-24 space-y-4 max-w-2xl">
      <div className="flex items-center gap-2">
        <Link to={`/stores/${storeId}`} className="p-1.5 -ml-1.5 rounded-lg hover:bg-surface-hover" aria-label={t('common:actions.back')}><ArrowLeft size={20} /></Link>
        <h1 className="text-xl font-bold text-text flex items-center gap-2"><Stamp size={20} className="text-amber-600" /> {t('store.title')}</h1>
      </div>

      <Gate capability="Loyalty">
        {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{error}</p>}

        {info.active && (
          <section className="bg-white rounded-2xl border border-border/60 p-4 space-y-3">
            <div className="flex gap-2">
              {tab('stamp', t('scan.stampTab'), Stamp)}
              {tab('redeem', t('scan.redeemTab'), Gift)}
            </div>
            <p className="text-xs text-text-secondary">{mode === 'stamp' ? t('scan.stampHint') : t('scan.redeemHint', { reward: info.rewardDescription })}</p>

            {result && (
              <div role="status" className="p-3 rounded-xl bg-green-50 text-green-800 text-sm space-y-2">
                <p className="flex items-center gap-1.5 font-semibold"><CheckCircle2 size={16} /> @{result.username} · {result.message}</p>
                <StampRow stamps={result.stamps} required={result.stampsRequired} />
                {result.mode === 'stamp' && result.rewardReady && <p className="font-semibold text-amber-800">{t('scan.rewardReady', { reward: info.rewardDescription })}</p>}
              </div>
            )}

            {scanning ? (
              <>
                <QrReader paused={busy} onRead={submit} />
                <Button variant="outline" className="w-full" onClick={() => setScanning(false)}>{t('scan.stop')}</Button>
              </>
            ) : (
              <Button className="w-full" onClick={() => setScanning(true)}><Camera size={16} /> {t('scan.start')}</Button>
            )}

            <form onSubmit={(e) => { e.preventDefault(); submit(code); }} className="flex gap-2">
              <input value={code} onChange={(e) => setCode(e.target.value)} placeholder={t('scan.codePlaceholder')} aria-label={t('scan.codePlaceholder')}
                autoCapitalize="characters" autoComplete="off" spellCheck={false}
                className="flex-1 px-3 py-2 rounded-xl border border-border text-sm font-mono uppercase placeholder:normal-case placeholder:font-sans" />
              <Button type="submit" variant="outline" size="sm" isLoading={busy}>{mode === 'stamp' ? t('scan.stampButton') : t('scan.redeemButton')}</Button>
            </form>
          </section>
        )}

        <form onSubmit={save} className="bg-white rounded-2xl border border-border/60 p-4 space-y-3">
          <h2 className="text-sm font-bold text-text">{t('store.settings')}</h2>
          <label className="block">
            <span className="text-xs font-semibold text-text-secondary">{t('store.stampsRequired')}</span>
            <input type="number" min={2} max={20} value={form.stampsRequired}
              onChange={(e) => setForm({ ...form, stampsRequired: Number(e.target.value) })}
              className="mt-1 w-24 px-3 py-2 rounded-xl border border-border text-sm" />
          </label>
          <label className="block">
            <span className="text-xs font-semibold text-text-secondary">{t('store.reward')}</span>
            <input value={form.rewardDescription} maxLength={120} placeholder={t('store.rewardPlaceholder')}
              onChange={(e) => setForm({ ...form, rewardDescription: e.target.value })}
              className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm" />
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
            {t('store.active')}
          </label>
          <p className="text-[11px] text-text-muted">{t('store.rules')}</p>
          {saved && <p role="status" className="text-xs text-green-700">{saved}</p>}
          <Button type="submit" isLoading={saving}>{t('store.save')}</Button>
        </form>

        {info.configured && (
          <section className="bg-white rounded-2xl border border-border/60 p-4">
            <h2 className="text-sm font-bold text-text mb-2">{t('stats.title')}</h2>
            {stats ? (
              <dl className="grid grid-cols-2 gap-3 text-sm">
                {([
                  ['customers', stats.customers], ['withTwoOrMore', stats.withTwoOrMore], ['rewardsReady', stats.rewardsReady],
                  ['redeemedThisMonth', stats.redeemedThisMonth], ['stampsThisMonth', stats.stampsThisMonth],
                ] as const).map(([k, v]) => (
                  <div key={k} className="p-3 rounded-xl bg-surface-dark">
                    <dt className="text-[11px] text-text-muted">{t(`stats.${k}`)}</dt>
                    <dd className="text-lg font-bold text-text tabular-nums">{k !== 'customers' && k !== 'stampsThisMonth' && v === 0 ? t('stats.fewer') : v}</dd>
                  </div>
                ))}
              </dl>
            ) : (
              <p className="text-xs text-text-muted">{t('stats.notEnough')}</p>
            )}
          </section>
        )}
      </Gate>
    </div>
  );
}
