import { useEffect, useRef, useCallback } from 'react';

interface UsePollingOptions {
  enabled: boolean;
  intervalMs: number;
  onVisibilityChange?: boolean;
}

export function usePolling(
  callback: () => void | Promise<void>,
  { enabled, intervalMs, onVisibilityChange = true }: UsePollingOptions
) {
  const savedCallback = useRef(callback);
  savedCallback.current = callback;

  const poll = useCallback(() => {
    savedCallback.current();
  }, []);

  useEffect(() => {
    if (!enabled) return;

    poll();
    const id = setInterval(poll, intervalMs);

    if (!onVisibilityChange) return () => clearInterval(id);

    const handleVisibility = () => {
      if (document.visibilityState === 'visible') {
        poll();
      }
    };

    document.addEventListener('visibilitychange', handleVisibility);

    return () => {
      clearInterval(id);
      document.removeEventListener('visibilitychange', handleVisibility);
    };
  }, [enabled, intervalMs, poll, onVisibilityChange]);

  return { refresh: poll };
}
