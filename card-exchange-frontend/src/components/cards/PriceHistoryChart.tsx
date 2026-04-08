import { useState, useEffect } from 'react';
import { XAxis, YAxis, Tooltip, ResponsiveContainer, Area, AreaChart } from 'recharts';
import { TrendingUp, TrendingDown, Minus, Loader2 } from 'lucide-react';
import { priceTracking } from '../../api';
import type { PricePoint, PriceDayPoint } from '../../types';

interface PriceHistoryChartProps {
  cardInfoId: number;
  /** If provided, uses this data (from spike detection) instead of fetching */
  spikeData?: {
    last5Days: PriceDayPoint[];
    changePercentage: number;
    changeAmount: number;
    currentPriceEur: number;
    oldPriceEur: number;
  };
}

export default function PriceHistoryChart({ cardInfoId, spikeData }: PriceHistoryChartProps) {
  const [dataPoints, setDataPoints] = useState<{ date: string; price: number }[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(false);
  const [changePct, setChangePct] = useState(0);
  const [changeAmt, setChangeAmt] = useState(0);

  useEffect(() => {
    if (spikeData) {
      const points = spikeData.last5Days
        .filter((d) => d.priceEur != null)
        .map((d) => ({
          date: new Date(d.date).toLocaleDateString('it-IT', { day: '2-digit', month: '2-digit' }),
          price: d.priceEur!,
        }));
      setDataPoints(points);
      setChangePct(spikeData.changePercentage);
      setChangeAmt(spikeData.changeAmount);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setError(false);

    priceTracking
      .history(cardInfoId, 30)
      .then(({ data }) => {
        if (cancelled) return;
        const points = (data.dataPoints || [])
          .filter((p: PricePoint) => p.priceEur != null)
          .map((p: PricePoint) => ({
            date: new Date(p.date).toLocaleDateString('it-IT', { day: '2-digit', month: '2-digit' }),
            price: p.priceEur!,
          }));
        setDataPoints(points);

        if (points.length >= 2) {
          const first = points[0].price;
          const last = points[points.length - 1].price;
          const amt = last - first;
          const pct = first > 0 ? (amt / first) * 100 : 0;
          setChangePct(Math.round(pct * 100) / 100);
          setChangeAmt(Math.round(amt * 100) / 100);
        }
      })
      .catch(() => {
        if (!cancelled) setError(true);
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => { cancelled = true; };
  }, [cardInfoId, spikeData]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-6">
        <Loader2 size={18} className="animate-spin text-text-muted" />
      </div>
    );
  }

  if (error || dataPoints.length < 2) {
    return (
      <div className="text-center py-4">
        <p className="text-[11px] text-text-muted">
          {error ? 'Impossibile caricare lo storico prezzi' : 'Dati insufficienti per il grafico'}
        </p>
      </div>
    );
  }

  const isUp = changePct > 0;
  const isDown = changePct < 0;
  const color = isUp ? '#22c55e' : isDown ? '#ef4444' : '#6b7280';
  const TrendIcon = isUp ? TrendingUp : isDown ? TrendingDown : Minus;

  return (
    <div>
      {/* Change summary */}
      <div className="flex items-center gap-2 mb-2">
        <TrendIcon size={14} className={isUp ? 'text-green-500' : isDown ? 'text-red-500' : 'text-gray-500'} />
        <span className={`text-xs font-bold ${isUp ? 'text-green-600' : isDown ? 'text-red-600' : 'text-gray-600'}`}>
          {isUp ? '+' : ''}{changePct}%
        </span>
        <span className="text-[11px] text-text-muted">
          ({isUp ? '+' : ''}{changeAmt.toFixed(2)}€)
        </span>
        <span className="text-[10px] text-text-muted ml-auto">
          {spikeData ? 'ultimi 5 giorni' : 'ultimi 30 giorni'}
        </span>
      </div>

      {/* Chart */}
      <div className="h-32">
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={dataPoints} margin={{ top: 4, right: 4, bottom: 0, left: 4 }}>
            <defs>
              <linearGradient id={`gradient-${cardInfoId}`} x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor={color} stopOpacity={0.2} />
                <stop offset="95%" stopColor={color} stopOpacity={0} />
              </linearGradient>
            </defs>
            <XAxis
              dataKey="date"
              tick={{ fontSize: 9, fill: '#9ca3af' }}
              axisLine={false}
              tickLine={false}
            />
            <YAxis
              domain={['auto', 'auto']}
              tick={{ fontSize: 9, fill: '#9ca3af' }}
              axisLine={false}
              tickLine={false}
              width={35}
              tickFormatter={(v: number) => `€${v}`}
            />
            <Tooltip
              contentStyle={{
                fontSize: '11px',
                borderRadius: '8px',
                border: '1px solid #e5e7eb',
                boxShadow: '0 2px 8px rgba(0,0,0,0.1)',
              }}
              formatter={(value: unknown) => [`€${Number(value).toFixed(2)}`, 'Prezzo']}
            />
            <Area
              type="monotone"
              dataKey="price"
              stroke={color}
              strokeWidth={2}
              fill={`url(#gradient-${cardInfoId})`}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
