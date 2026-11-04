import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Area, AreaChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { ArrowLeft, Crown, Download, FileJson, FileSpreadsheet, Info, Loader2, Lock, TrendingDown, TrendingUp } from 'lucide-react';
import { billing, downloadExport, statistics } from '../api';
import type { BasicStats, CollectionInsights, StatsCardLine, ValueChange } from '../types';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';

const euro = (v: number) => v.toLocaleString('it-IT', { style: 'currency', currency: 'EUR' });
const shortDate = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
const CONDITION_IT: Record<string, string> = {
  Mint: 'Mint', NearMint: 'Near Mint', Excellent: 'Excellent', Good: 'Good',
  LightlyPlayed: 'Lightly Played', ModeratelyPlayed: 'Moderately Played', HeavilyPlayed: 'Heavily Played', Damaged: 'Damaged',
};
const RANGES = [30, 90, 365] as const;
// Colore del grafico: il primario dell'app, unico per la sola serie mostrata
const CHART = { line: '#1a3461', grid: '#dde4ee', tick: '#6b7a99' };

function Tile({ label, value, sub }: { label: string; value: string; sub?: React.ReactNode }) {
  return (
    <div className="bg-white rounded-2xl p-3 border border-border/60">
      <p className="text-[11px] text-text-muted">{label}</p>
      <p className="text-lg font-bold text-text leading-tight">{value}</p>
      {sub && <div className="text-[11px] mt-0.5">{sub}</div>}
    </div>
  );
}

function Delta({ change, label }: { change: ValueChange; label: string }) {
  if (change.amount === 0) return <span className="text-text-muted">invariato {label}</span>;
  const up = change.amount > 0;
  const Icon = up ? TrendingUp : TrendingDown;
  return (
    <span className={`inline-flex items-center gap-1 font-semibold ${up ? 'text-green-700' : 'text-red-700'}`}>
      <Icon size={12} />
      {up ? '+' : '−'}{euro(Math.abs(change.amount))}
      {change.percent != null && <span className="font-normal">({up ? '+' : ''}{change.percent}%)</span>}
      <span className="font-normal text-text-muted">{label}</span>
    </span>
  );
}

function CardRow({ c, mode }: { c: StatsCardLine; mode: 'value' | 'change' }) {
  const up = (c.totalChange ?? 0) > 0;
  return (
    <li className="flex items-center gap-3 py-2">
      {c.imageUrl ? (
        <img src={c.imageUrl} alt="" loading="lazy" className="w-9 h-12 rounded object-cover bg-surface-dark shrink-0" />
      ) : (
        <span className="w-9 h-12 rounded bg-surface-dark shrink-0" />
      )}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium text-text truncate">{c.name}</p>
        <p className="text-[11px] text-text-muted truncate">
          {c.setName}{c.finish !== 'Normal' ? ` · ${c.finish === 'ReverseHolo' ? 'Reverse Holo' : c.finish}` : ''}
          {c.condition ? ` · ${CONDITION_IT[c.condition] ?? c.condition}` : ''}{c.quantity > 1 ? ` · ×${c.quantity}` : ''}
        </p>
      </div>
      {mode === 'value' ? (
        <div className="text-right shrink-0">
          <p className="text-sm font-semibold text-text">{euro(c.unitValue)}</p>
          {c.quantity > 1 && c.totalValue != null && <p className="text-[11px] text-text-muted">tot. {euro(c.totalValue)}</p>}
        </div>
      ) : (
        <div className="text-right shrink-0">
          <p className={`text-sm font-semibold ${up ? 'text-green-700' : 'text-red-700'}`}>
            {up ? '+' : '−'}{euro(Math.abs(c.totalChange ?? 0))}
          </p>
          <p className="text-[11px] text-text-muted">{up ? '+' : ''}{c.percent}% · ora {euro(c.unitValue)}</p>
        </div>
      )}
    </li>
  );
}

function ExportPanel() {
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const run = async (path: 'collection/csv' | 'collection/json' | 'wishlist/csv') => {
    setBusy(path);
    setError(null);
    try {
      await downloadExport(path);
    } catch (err) {
      setError(apiErrorMessage(err, 'Esportazione non riuscita.'));
    } finally {
      setBusy(null);
    }
  };
  return (
    <section className="bg-white rounded-2xl p-4 border border-border/60 space-y-3">
      <h2 className="text-sm font-bold text-text flex items-center gap-1.5"><Download size={16} /> Esporta i tuoi dati</h2>
      <p className="text-xs text-text-secondary">
        Il CSV della collezione si può reimportare in CardExchange e nelle principali app (ManaBox, Moxfield). Contiene anche valore di mercato e prezzo che hai indicato.
      </p>
      <div className="grid grid-cols-1 gap-2">
        <Button variant="outline" onClick={() => run('collection/csv')} isLoading={busy === 'collection/csv'} disabled={!!busy}>
          <FileSpreadsheet size={16} /> Collezione (CSV)
        </Button>
        <Button variant="outline" onClick={() => run('collection/json')} isLoading={busy === 'collection/json'} disabled={!!busy}>
          <FileJson size={16} /> Collezione (JSON)
        </Button>
        <Button variant="outline" onClick={() => run('wishlist/csv')} isLoading={busy === 'wishlist/csv'} disabled={!!busy}>
          <FileSpreadsheet size={16} /> Wishlist (CSV)
        </Button>
      </div>
      {error && <p className="text-xs text-danger">{error}</p>}
    </section>
  );
}

export default function StatsPage() {
  const navigate = useNavigate();
  const [basic, setBasic] = useState<BasicStats | null>(null);
  const [premium, setPremium] = useState<boolean | null>(null);
  const [days, setDays] = useState<(typeof RANGES)[number]>(90);
  const [insights, setInsights] = useState<CollectionInsights | null>(null);
  const [loadedDays, setLoadedDays] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    statistics.basic().then(({ data }) => setBasic(data)).catch((err) => setError(apiErrorMessage(err, 'Statistiche non disponibili.')));
    billing.current().then(({ data }) => setPremium(data.tier !== 'Free')).catch(() => setPremium(false));
  }, []);

  useEffect(() => {
    if (!premium) return;
    let cancelled = false;
    statistics.insights(days)
      .then(({ data }) => { if (!cancelled) setInsights(data); })
      .catch((err) => { if (!cancelled) setError(apiErrorMessage(err, 'Statistiche non disponibili.')); })
      .finally(() => { if (!cancelled) setLoadedDays(days); });
    return () => { cancelled = true; };
  }, [premium, days]);
  const loadingInsights = !!premium && loadedDays !== days;

  const header = (
    <div className="flex items-center gap-3">
      <button onClick={() => navigate(-1)} aria-label="Indietro" className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0">
        <ArrowLeft size={18} />
      </button>
      <h1 className="text-xl font-bold text-text flex-1">Statistiche</h1>
    </div>
  );

  if (!basic || premium === null) {
    return (
      <div className="max-w-lg mx-auto space-y-4">
        {header}
        <div className="flex justify-center py-16">{error ? <p className="text-sm text-danger">{error}</p> : <Loader2 className="animate-spin text-primary" />}</div>
      </div>
    );
  }

  const t = insights?.totals;
  const series = insights?.series ?? [];
  const values = series.map((p) => p.value);
  const flat = values.length > 0 && Math.max(...values) === Math.min(...values);
  // Con variazioni piccole servono i centesimi, altrimenti le tacche dell'asse si ripetono
  const span = values.length > 0 ? Math.max(...values) - Math.min(...values) : 0;

  return (
    <div className="max-w-lg mx-auto pb-24 space-y-4">
      {header}
      {error && <div className="p-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}

      <div className="grid grid-cols-2 gap-3">
        <Tile
          label="Valore di mercato"
          value={euro(t?.marketValue ?? basic.marketValue)}
          sub={insights ? <Delta change={insights.change7d} label="in 7 giorni" /> : undefined}
        />
        <Tile
          label="Carte"
          value={(t?.totalCards ?? basic.totalCards).toLocaleString('it-IT')}
          sub={<span className="text-text-muted">{(t?.uniqueCards ?? basic.uniqueCards).toLocaleString('it-IT')} diverse</span>}
        />
        <Tile
          label="In scambio"
          value={(t?.availableForTrade ?? basic.availableForTrade).toLocaleString('it-IT')}
          sub={t ? <span className="text-text-muted">valgono {euro(t.tradeableValue)}</span> : undefined}
        />
        <Tile
          label="Wishlist"
          value={basic.wishlistItems.toLocaleString('it-IT')}
          sub={<span className="text-text-muted">{basic.pendingTrades} proposte in corso</span>}
        />
      </div>

      {!premium ? (
        <>
          <section className="bg-white rounded-2xl p-4 border-2 border-secondary space-y-2">
            <h2 className="text-base font-bold text-text flex items-center gap-1.5"><Lock size={16} className="text-amber-700" /> Con Premium vedi di più</h2>
            <ul className="text-sm text-text-secondary space-y-1 list-disc pl-5">
              <li>Andamento del valore della collezione nel tempo</li>
              <li>Le carte che sono salite o scese di più in settimana</li>
              <li>Le tue carte di maggior valore, per gioco e condizione</li>
              <li>Esportazione di collezione e wishlist</li>
            </ul>
            <Link to="/premium" className="inline-flex items-center gap-1.5 mt-1 px-4 py-2.5 rounded-xl bg-primary text-white text-sm font-semibold">
              <Crown size={16} /> Scopri Premium
            </Link>
          </section>
        </>
      ) : loadingInsights && !insights ? (
        <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>
      ) : insights && t ? (
        <>
          <section className="bg-white rounded-2xl p-4 border border-border/60 space-y-3">
            <div className="flex items-center justify-between gap-2">
              <h2 className="text-sm font-bold text-text">Andamento del valore</h2>
              <div className="flex rounded-lg bg-surface-dark p-0.5 text-xs" role="group" aria-label="Periodo">
                {RANGES.map((r) => (
                  <button
                    key={r}
                    onClick={() => setDays(r)}
                    aria-pressed={days === r}
                    className={`px-2.5 py-1 rounded-md font-medium ${days === r ? 'bg-white shadow-sm text-text' : 'text-text-secondary'}`}
                  >
                    {r === 365 ? '1 anno' : `${r} gg`}
                  </button>
                ))}
              </div>
            </div>
            <p className="text-xs"><Delta change={insights.change30d} label="in 30 giorni" /></p>
            <div className="h-44" aria-label={`Valore della collezione negli ultimi ${days} giorni`}>
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={series} margin={{ top: 6, right: 6, bottom: 0, left: 0 }}>
                  <defs>
                    <linearGradient id="valueFill" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor={CHART.line} stopOpacity={0.18} />
                      <stop offset="100%" stopColor={CHART.line} stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <XAxis dataKey="date" tickFormatter={shortDate} tick={{ fontSize: 10, fill: CHART.tick }} axisLine={false} tickLine={false} minTickGap={28} />
                  <YAxis
                    domain={flat ? [0, 'auto'] : ['auto', 'auto']}
                    tick={{ fontSize: 10, fill: CHART.tick }}
                    axisLine={false}
                    tickLine={false}
                    width={span < 20 ? 66 : 52}
                    tickFormatter={(v: number) => `${v.toLocaleString('it-IT', { maximumFractionDigits: span < 20 ? 2 : 0, minimumFractionDigits: span < 20 ? 2 : 0 })} €`}
                  />
                  <Tooltip
                    cursor={{ stroke: CHART.grid, strokeWidth: 1 }}
                    contentStyle={{ fontSize: 12, borderRadius: 10, border: `1px solid ${CHART.grid}` }}
                    labelFormatter={(l) => new Date(`${l}T00:00:00`).toLocaleDateString('it-IT', { day: 'numeric', month: 'long', year: 'numeric' })}
                    formatter={(v) => [euro(Number(v)), 'Valore']}
                  />
                  <Area type="monotone" dataKey="value" stroke={CHART.line} strokeWidth={2} fill="url(#valueFill)" dot={false} activeDot={{ r: 4, strokeWidth: 2, stroke: '#fff' }} />
                </AreaChart>
              </ResponsiveContainer>
            </div>
            <p className="text-[11px] text-text-muted flex gap-1">
              <Info size={12} className="shrink-0 mt-0.5" />
              Valore di mercato delle carte che possiedi oggi, con condizione e finitura. È un riferimento: negli scambi vi accordate liberamente.
              {t.unpricedCards > 0 && ` ${t.unpricedCards} carte senza prezzo non sono conteggiate.`}
            </p>
          </section>

          {(insights.gainers.length > 0 || insights.losers.length > 0) && (
            <section className="bg-white rounded-2xl p-4 border border-border/60">
              <h2 className="text-sm font-bold text-text mb-1">Variazioni della settimana</h2>
              {insights.gainers.length > 0 && (
                <>
                  <p className="text-[11px] font-semibold text-green-700 mt-2 flex items-center gap-1"><TrendingUp size={12} /> In salita</p>
                  <ul className="divide-y divide-border/60">{insights.gainers.map((c) => <CardRow key={`g${c.cardId}`} c={c} mode="change" />)}</ul>
                </>
              )}
              {insights.losers.length > 0 && (
                <>
                  <p className="text-[11px] font-semibold text-red-700 mt-2 flex items-center gap-1"><TrendingDown size={12} /> In discesa</p>
                  <ul className="divide-y divide-border/60">{insights.losers.map((c) => <CardRow key={`l${c.cardId}`} c={c} mode="change" />)}</ul>
                </>
              )}
            </section>
          )}

          {insights.topCards.length > 0 && (
            <section className="bg-white rounded-2xl p-4 border border-border/60">
              <h2 className="text-sm font-bold text-text mb-1">Le carte di maggior valore</h2>
              <ul className="divide-y divide-border/60">{insights.topCards.map((c) => <CardRow key={c.cardId} c={c} mode="value" />)}</ul>
            </section>
          )}

          {insights.byGame.length > 0 && (
            <section className="bg-white rounded-2xl p-4 border border-border/60 space-y-3">
              <h2 className="text-sm font-bold text-text">Per gioco</h2>
              {insights.byGame.map((g) => {
                const share = t.marketValue > 0 ? (g.value / t.marketValue) * 100 : 0;
                return (
                  <div key={g.game}>
                    <div className="flex justify-between text-xs mb-1">
                      <span className="text-text font-medium">{g.game} <span className="text-text-muted font-normal">· {g.count} carte</span></span>
                      <span className="text-text">{euro(g.value)}</span>
                    </div>
                    <div className="h-2 rounded-full bg-surface-dark overflow-hidden">
                      <div className="h-full rounded-full" style={{ width: `${Math.max(share, g.value > 0 ? 2 : 0)}%`, background: CHART.line }} />
                    </div>
                  </div>
                );
              })}
            </section>
          )}

          {insights.byCondition.length > 0 && (
            <section className="bg-white rounded-2xl p-4 border border-border/60">
              <h2 className="text-sm font-bold text-text mb-2">Per condizione</h2>
              <table className="w-full text-xs">
                <thead>
                  <tr className="text-text-muted text-left">
                    <th className="font-normal pb-1">Condizione</th>
                    <th className="font-normal pb-1 text-right">Carte</th>
                    <th className="font-normal pb-1 text-right">Valore</th>
                  </tr>
                </thead>
                <tbody>
                  {insights.byCondition.map((c) => (
                    <tr key={c.condition} className="border-t border-border/60">
                      <td className="py-1.5 text-text">{CONDITION_IT[c.condition] ?? c.condition}</td>
                      <td className="py-1.5 text-right text-text">{c.count}</td>
                      <td className="py-1.5 text-right text-text">{euro(c.value)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}

          <ExportPanel />
        </>
      ) : null}
    </div>
  );
}
