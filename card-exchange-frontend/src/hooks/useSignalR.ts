import { useEffect, useRef, useCallback } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel, HttpTransportType } from '@microsoft/signalr';

const HUB_URL = '/hubs/notifications';

export function useSignalR(
  enabled: boolean,
  onNotification: (notification: any) => void,
) {
  const connectionRef = useRef<HubConnection | null>(null);
  const onNotificationRef = useRef(onNotification);
  onNotificationRef.current = onNotification;

  const getToken = useCallback(() => {
    return localStorage.getItem('token') || '';
  }, []);

  useEffect(() => {
    if (!enabled) return;

    const token = getToken();
    if (!token) return;

    const baseUrl = import.meta.env.VITE_API_URL || '';
    const connection = new HubConnectionBuilder()
      .withUrl(`${baseUrl}${HUB_URL}`, {
        accessTokenFactory: () => getToken(),
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('ReceiveNotification', (notification: any) => {
      onNotificationRef.current(notification);
    });

    connection.start()
      .catch((err) => console.warn('SignalR connection failed, falling back to polling:', err));

    connectionRef.current = connection;

    return () => {
      connection.stop().catch(() => {});
      connectionRef.current = null;
    };
  }, [enabled, getToken]);

  return connectionRef;
}
