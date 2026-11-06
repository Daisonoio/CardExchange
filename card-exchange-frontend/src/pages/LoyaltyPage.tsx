import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import QRCode from 'qrcode';
import { ArrowLeft, Gift, Loader2, RefreshCw, Stamp } from 'lucide-react';
import { loyalty, type LoyaltyCardInfo } from '../api';
import EmptyState from '../components/ui/EmptyState';
import EmptyStateLink from '../components/ui/EmptyStateLink';
import StampRow from '../components/loyalty/StampRow';

/**
 * Codice personale da mostrare in cassa: QR più codice da digitare. Vale 2 minuti e si rinnova da solo,
 * così uno screenshot non si può riusare.
 */
function PassCode() {
  const { t } = useTranslation('loyalty');
  const [pass, setPass] = useState<{ token: string; expiresAt: number } | null>(null);
  const [svg, setSvg] = useState('');
  const [left, setLeft] = useState(0);
  const [failed, setFailed] = useState(false);

  const loading = useRef(false);
  const refresh = useCallback(async () => {
    if (loading.current) return;
    loading.current = true;
    try {
      const { data } = await loyalty.pass();
      setPass({ token: data.token, expiresAt: new Date(data.expiresAt).getTime() });
      setSvg(await QRCode.toString(data.token.toUpperCase(), { type: 'svg', errorCorrectionLevel: 'M', margin: 1, color: { dark: '#122548', light: '#ffffff' } }));
      setFailed(false);
    } catch {
      setFailed(true);
    } finally {
      loading.current = false;
    }
  }, []);

  useEffect(() => {
    const first = setTimeout(refresh, 0);
    return () => clearTimeout(first);
  }, [refresh]);
  useEffect(() => {
    if (!pass) return;
    const tick = () => {
      const s = Math.max(0, Math.round((pass.expiresAt - Date.now()) / 1000));
      setLeft(s);
      // Si rinnova un po' prima della scadenza, così in cassa è sempre valido
      if (s <= 15) refresh();
    };
    const first = setTimeout(tick, 0);
    const timer = setInterval(tick, 1000);
    return () => { clearTimeout(first); clearInterval(timer); };
  }, [pass, refresh]);

  if (failed) return <button type="button" onClick={refresh} className="text-sm text-primary font-semibold">{t('pass.retry')}</button>;
  if (!pass) return <div className="flex justify-center py-8"><Loader2 className="animate-spin text-primary" /></div>;
  const code = pass.token.toUpperCase();
  return (
    <div className="flex flex-col items-center gap-2">
      <div className="w-56 h-56 bg-white p-2 rounded-2xl border border-border [&>svg]:w-full [&>svg]:h-full" role="img" aria-label={t('pass.qrLabel')} dangerouslySetInnerHTML={{ __html: svg }} />
      <p className="font-mono text-base tracking-wider text-text select-all" data-testid="loyalty-code">{code}</p>
      <p className="text-xs text-text-muted flex items-center gap-1.5">
        <RefreshCw size={12} /> {t('pass.renews', { seconds: left })}
      </p>
    </div>
  );
}

export default function LoyaltyPage() {
  const { t } = useTranslation('loyalty');
  const [cards, setCards] = useState<LoyaltyCardInfo[] | null>(null);

  useEffect(() => {
    loyalty.mine().then(({ data }) => setCards(data)).catch(() => setCards([]));
  }, []);

  return (
    <div className="pb-24 space-y-4 max-w-xl">
      <div className="flex items-center gap-2">
        <Link to="/profile" className="p-1.5 -ml-1.5 rounded-lg hover:bg-surface-hover" aria-label={t('common:actions.back')}><ArrowLeft size={20} /></Link>
        <h1 className="text-xl font-bold text-text flex items-center gap-2"><Stamp size={20} className="text-amber-600" /> {t('title')}</h1>
      </div>

      <section className="bg-white rounded-2xl border border-border/60 p-4 space-y-3">
        <div>
          <h2 className="text-sm font-bold text-text">{t('pass.title')}</h2>
          <p className="text-xs text-text-secondary">{t('pass.hint')}</p>
        </div>
        <PassCode />
      </section>

      {cards === null ? (
        <div className="flex justify-center py-8"><Loader2 className="animate-spin text-primary" /></div>
      ) : cards.length === 0 ? (
        <EmptyState icon={Stamp} title={t('empty.title')} description={t('empty.description')}
          action={<EmptyStateLink to="/local" label={t('empty.action')} />} />
      ) : (
        <ul className="space-y-3">
          {cards.map((c) => (
            <li key={c.storeId} className={`bg-white rounded-2xl border p-4 space-y-2 ${c.rewardReady ? 'border-amber-400' : 'border-border/60'}`}>
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <Link to={`/stores/${c.storeId}`} className="block text-sm font-bold text-text truncate hover:underline">{c.storeName}</Link>
                  <span className="text-xs text-text-muted">{c.city}</span>
                </div>
                <span className="text-xs font-semibold text-text-secondary tabular-nums">{t('card.count', { stamps: Math.min(c.stamps, c.stampsRequired), required: c.stampsRequired })}</span>
              </div>
              <StampRow stamps={c.stamps} required={c.stampsRequired} />
              <p className={`text-xs flex items-center gap-1.5 ${c.rewardReady ? 'text-amber-800 font-semibold' : 'text-text-secondary'}`}>
                <Gift size={13} /> {c.rewardReady ? t('card.ready', { reward: c.reward }) : t('card.reward', { reward: c.reward, count: c.stampsRequired - c.stamps })}
              </p>
              {c.rewardsRedeemed > 0 && <p className="text-[11px] text-text-muted">{t('card.redeemed', { count: c.rewardsRedeemed })}</p>}
            </li>
          ))}
        </ul>
      )}
      <p className="text-[11px] text-text-muted">{t('privacy')}</p>
    </div>
  );
}
