import { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react';
import type { ReactNode } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel, HttpTransportType } from '@microsoft/signalr';
import { useAuth } from './AuthContext';
import { messages, notifications } from '../api';

interface RealtimeContextValue {
  chatUnread: number;
  notifUnread: number;
  setChatUnread: React.Dispatch<React.SetStateAction<number>>;
  setNotifUnread: React.Dispatch<React.SetStateAction<number>>;
  refreshChatCount: () => Promise<void>;
  refreshNotifCount: () => Promise<void>;
}

const RealtimeContext = createContext<RealtimeContextValue>({
  chatUnread: 0,
  notifUnread: 0,
  setChatUnread: () => {},
  setNotifUnread: () => {},
  refreshChatCount: async () => {},
  refreshNotifCount: async () => {},
});

export function useRealtime() {
  return useContext(RealtimeContext);
}

const POLL_FALLBACK_MS = 60_000;

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const [chatUnread, setChatUnread] = useState(0);
  const [notifUnread, setNotifUnread] = useState(0);
  const notifHubRef = useRef<HubConnection | null>(null);
  const chatHubRef = useRef<HubConnection | null>(null);

  const getToken = useCallback(() => localStorage.getItem('token') || '', []);

  const refreshChatCount = useCallback(async () => {
    try {
      const { data } = await messages.getUnreadCount();
      setChatUnread(data?.unreadCount ?? 0);
    } catch {}
  }, []);

  const refreshNotifCount = useCallback(async () => {
    try {
      const { data } = await notifications.getUnreadCount();
      setNotifUnread(data?.unreadCount ?? (data as any)?.UnreadCount ?? 0);
    } catch {}
  }, []);

  // Initial fetch + slow polling fallback
  useEffect(() => {
    if (!user) {
      setChatUnread(0);
      setNotifUnread(0);
      return;
    }
    refreshChatCount();
    refreshNotifCount();
    const id = setInterval(() => {
      refreshChatCount();
      refreshNotifCount();
    }, POLL_FALLBACK_MS);
    return () => clearInterval(id);
  }, [user, refreshChatCount, refreshNotifCount]);

  // Notification hub — real-time
  useEffect(() => {
    if (!user) return;
    const baseUrl = import.meta.env.VITE_API_URL || '';
    const conn = new HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/notifications`, {
        accessTokenFactory: () => getToken(),
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    conn.on('ReceiveNotification', () => {
      refreshNotifCount();
    });

    conn.onreconnected(() => {
      refreshNotifCount();
      refreshChatCount();
    });

    conn.start().catch((err) =>
      console.warn('Notification hub failed, falling back to polling:', err)
    );
    notifHubRef.current = conn;

    return () => {
      conn.stop().catch(() => {});
      notifHubRef.current = null;
    };
  }, [user, getToken, refreshNotifCount, refreshChatCount]);

  // Chat hub — real-time badge updates
  useEffect(() => {
    if (!user) return;
    const baseUrl = import.meta.env.VITE_API_URL || '';
    const conn = new HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/chat`, {
        accessTokenFactory: () => getToken(),
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    conn.on('NewMessageAlert', () => {
      refreshChatCount();
    });

    conn.on('ReceiveMessage', () => {
      refreshChatCount();
    });

    conn.on('MessagesRead', () => {
      refreshChatCount();
    });

    conn.onreconnected(() => {
      refreshChatCount();
    });

    conn.start().catch((err) =>
      console.warn('Chat hub failed, falling back to polling:', err)
    );
    chatHubRef.current = conn;

    return () => {
      conn.stop().catch(() => {});
      chatHubRef.current = null;
    };
  }, [user, getToken, refreshChatCount]);

  return (
    <RealtimeContext.Provider
      value={{
        chatUnread,
        notifUnread,
        setChatUnread,
        setNotifUnread,
        refreshChatCount,
        refreshNotifCount,
      }}
    >
      {children}
    </RealtimeContext.Provider>
  );
}
