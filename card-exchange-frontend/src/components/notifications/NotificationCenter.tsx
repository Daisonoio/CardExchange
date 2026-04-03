import { useState, useCallback, useRef } from 'react';
import { Bell, Check, CheckCheck, Trash2, Settings } from 'lucide-react';
import { notifications } from '../../api';
import type { Notification } from '../../types';
import { getNotificationConfig } from './notificationRegistry';
import { usePolling } from '../../hooks/usePolling';
import NotificationPreferences from './NotificationPreferences';
import BottomSheet from '../ui/BottomSheet';

const POLL_INTERVAL = 10_000;

interface NotificationCenterProps {
  open: boolean;
  onClose: () => void;
  onUnreadCountChange?: (count: number) => void;
}

export default function NotificationCenter({ open, onClose, onUnreadCountChange }: NotificationCenterProps) {
  const [items, setItems] = useState<Notification[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showPreferences, setShowPreferences] = useState(false);
  const hasDoneInitialLoad = useRef(false);

  const loadNotifications = useCallback(async () => {
    if (!hasDoneInitialLoad.current) setIsLoading(true);
    try {
      const { data } = await notifications.getAll({ pageSize: 50 });
      const list = data?.notifications ?? (data as any)?.Notifications ?? [];
      setItems(Array.isArray(list) ? list : []);
      const unread = list.filter((n: Notification) => !n.isRead).length;
      onUnreadCountChange?.(unread);
    } catch (err) {
      console.error('Errore caricamento notifiche:', err);
    } finally {
      setIsLoading(false);
      hasDoneInitialLoad.current = true;
    }
  }, [onUnreadCountChange]);

  usePolling(loadNotifications, {
    enabled: open,
    intervalMs: POLL_INTERVAL,
    onVisibilityChange: true,
  });

  const handleMarkAsRead = async (id: number) => {
    try {
      await notifications.markAsRead(id);
      setItems((prev) => prev.map((n) => n.id === id ? { ...n, isRead: true } : n));
      onUnreadCountChange?.(items.filter((n) => !n.isRead && n.id !== id).length);
    } catch {}
  };

  const handleMarkAllRead = async () => {
    try {
      await notifications.markAllAsRead();
      setItems((prev) => prev.map((n) => ({ ...n, isRead: true })));
      onUnreadCountChange?.(0);
    } catch {}
  };

  const handleDelete = async (id: number) => {
    try {
      await notifications.delete(id);
      setItems((prev) => prev.filter((n) => n.id !== id));
    } catch {}
  };

  const unreadCount = items.filter((n) => !n.isRead).length;

  return (
    <>
    <BottomSheet open={open} onClose={onClose} title="Notifiche">
      <div className="space-y-2">
        <div className="flex items-center justify-between mb-2">
          {unreadCount > 0 && (
            <span className="text-xs text-text-muted">{unreadCount} non lette</span>
          )}
          <div className="flex items-center gap-2 ml-auto">
            {unreadCount > 0 && (
              <button
                onClick={handleMarkAllRead}
                className="text-xs text-primary font-medium hover:underline flex items-center gap-1"
              >
                <CheckCheck size={12} /> Segna tutte
              </button>
            )}
            <button
              onClick={() => setShowPreferences(true)}
              className="p-1.5 rounded-lg text-text-muted hover:bg-gray-100 hover:text-text"
              title="Preferenze notifiche"
            >
              <Settings size={14} />
            </button>
          </div>
        </div>

        {isLoading ? (
          <div className="space-y-2">
            {[...Array(3)].map((_, i) => (
              <div key={i} className="h-16 bg-gray-100 rounded-xl animate-pulse" />
            ))}
          </div>
        ) : items.length === 0 ? (
          <div className="text-center py-8">
            <Bell size={32} className="mx-auto text-text-muted mb-2" />
            <p className="text-sm text-text-muted">Nessuna notifica</p>
          </div>
        ) : (
          items.map((notification) => {
            const cfg = getNotificationConfig(notification.type);
            const Icon = cfg.icon;
            return (
              <div
                key={notification.id}
                className={`flex items-start gap-3 p-3 rounded-xl border transition-colors ${
                  notification.isRead
                    ? 'bg-white border-border/30'
                    : 'bg-blue-50/40 border-primary/20'
                }`}
              >
                <div className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 ${cfg.color}`}>
                  <Icon size={14} />
                </div>
                <div className="flex-1 min-w-0">
                  <p className={`text-xs font-semibold truncate ${notification.isRead ? 'text-text-secondary' : 'text-text'}`}>
                    {notification.title}
                  </p>
                  {notification.body && (
                    <p className="text-[11px] text-text-muted mt-0.5 line-clamp-2">{notification.body}</p>
                  )}
                  <p className="text-[10px] text-text-muted mt-1">
                    {new Date(notification.createdAt).toLocaleDateString('it-IT', {
                      day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit',
                    })}
                  </p>
                </div>
                <div className="flex flex-col gap-1 shrink-0">
                  {!notification.isRead && (
                    <button
                      onClick={() => handleMarkAsRead(notification.id)}
                      className="p-1 rounded text-primary hover:bg-primary/10"
                      title="Segna come letta"
                    >
                      <Check size={12} />
                    </button>
                  )}
                  <button
                    onClick={() => handleDelete(notification.id)}
                    className="p-1 rounded text-text-muted hover:bg-red-50 hover:text-red-500"
                    title="Elimina"
                  >
                    <Trash2 size={12} />
                  </button>
                </div>
              </div>
            );
          })
        )}
      </div>
    </BottomSheet>
    <NotificationPreferences
      open={showPreferences}
      onClose={() => setShowPreferences(false)}
    />
    </>
  );
}
