import { useState, useCallback } from 'react';
import { notifications } from '../api';
import { usePolling } from './usePolling';
import { useSignalR } from './useSignalR';

const POLL_INTERVAL = 30_000; // Polling come fallback (più lento con SignalR attivo)

export function useNotifications(enabled: boolean) {
  const [unreadCount, setUnreadCount] = useState(0);

  const fetchCount = useCallback(async () => {
    if (!enabled) return;
    try {
      const { data } = await notifications.getUnreadCount();
      setUnreadCount(data?.unreadCount ?? (data as any)?.UnreadCount ?? 0);
    } catch {
      // silently fail
    }
  }, [enabled]);

  // Polling come fallback
  const { refresh } = usePolling(fetchCount, {
    enabled,
    intervalMs: POLL_INTERVAL,
  });

  // SignalR real-time — incrementa il contatore quando arriva una notifica push
  useSignalR(enabled, () => {
    setUnreadCount((prev) => prev + 1);
  });

  return { unreadCount, setUnreadCount, refresh };
}
