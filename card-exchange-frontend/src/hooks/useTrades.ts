import { useState, useCallback } from 'react';
import { tradeOffers } from '../api';
import { usePolling } from './usePolling';
import type { TradeOffer } from '../types';

const POLL_INTERVAL = 15_000;

export function useTrades(enabled: boolean) {
  const [offers, setOffers] = useState<TradeOffer[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [hasDoneInitialLoad, setHasDoneInitialLoad] = useState(false);

  const fetchOffers = useCallback(async () => {
    if (!enabled) return;
    if (!hasDoneInitialLoad) setIsLoading(true);
    try {
      const { data } = await tradeOffers.getMine({ pageSize: 50 });
      const items = Array.isArray(data) ? data : (data as any)?.items ?? [];
      setOffers(Array.isArray(items) ? items : []);
    } catch {
      // silently fail on poll
    } finally {
      setIsLoading(false);
      setHasDoneInitialLoad(true);
    }
  }, [enabled, hasDoneInitialLoad]);

  const { refresh } = usePolling(fetchOffers, {
    enabled,
    intervalMs: POLL_INTERVAL,
  });

  return { offers, isLoading, refresh };
}
