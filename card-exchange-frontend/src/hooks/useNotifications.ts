import { useState, useCallback } from 'react';
import { notifications } from '../api';
import { usePolling } from './usePolling';

const POLL_INTERVAL = 15_000;

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

  const { refresh } = usePolling(fetchCount, {
    enabled,
    intervalMs: POLL_INTERVAL,
  });

  return { unreadCount, setUnreadCount, refresh };
}
