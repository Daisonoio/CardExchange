import { useState, useEffect, useCallback } from 'react';
import { notifications } from '../api';

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

  useEffect(() => {
    fetchCount();
    // Poll every 30 seconds
    const interval = setInterval(fetchCount, 30_000);
    return () => clearInterval(interval);
  }, [fetchCount]);

  return { unreadCount, setUnreadCount, refresh: fetchCount };
}
