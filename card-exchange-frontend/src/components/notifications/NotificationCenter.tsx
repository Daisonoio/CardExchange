import { useState, useEffect, useCallback } from 'react';
import { Bell, Check, CheckCheck, Trash2, ArrowLeftRight, Heart, MessageSquare, AlertTriangle, X } from 'lucide-react';
import { notifications } from '../../api';
import type { Notification } from '../../types';
import BottomSheet from '../ui/BottomSheet';

interface NotificationCenterProps {
  open: boolean;
  onClose: () => void;
  onUnreadCountChange?: (count: number) => void;
}

const TYPE_CONFIG: Record<string, { icon: typeof Bell; color: string }> = {
  TradeOfferReceived: { icon: ArrowLeftRight, color: 'text-blue-500 bg-blue-50' },
  TradeOfferAccepted: { icon: Check, color: 'text-green-500 bg-green-50' },
  TradeOfferRejected: { icon: X, color: 'text-red-500 bg-red-50' },
  TradeCompleted: { icon: CheckCheck, color: 'text-emerald-500 bg-emerald-50' },
  CounterOfferReceived: { icon: ArrowLeftRight, color: 'text-purple-500 bg-purple-50' },
  FavoritePriceChanged: { icon: Heart, color: 'text-amber-500 bg-amber-50' },
  FavoriteCardTraded: { icon: Heart, color: 'text-red-500 bg-red-50' },
  WishlistMatch: { icon: Heart, color: 'text-pink-500 bg-pink-50' },
  NewMessage: { icon: MessageSquare, color: 'text-indigo-500 bg-indigo-50' },
  NewReview: { icon: Check, color: 'text-teal-500 bg-teal-50' },
  SystemAnnouncement: { icon: AlertTriangle, color: 'text-gray-500 bg-gray-50' },
};

export default function NotificationCenter({ open, onClose, onUnreadCountChange }: NotificationCenterProps) {
  const [items, setItems] = useState<Notification[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const loadNotifications = useCallback(async () => {
    setIsLoading(true);
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
    }
  }, [onUnreadCountChange]);

  useEffect(() => {
    if (open) loadNotifications();
  }, [open, loadNotifications]);

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
    <BottomSheet open={open} onClose={onClose} title="Notifiche">
      <div className="space-y-2">
        {unreadCount > 0 && (
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs text-text-muted">{unreadCount} non lette</span>
            <button
              onClick={handleMarkAllRead}
              className="text-xs text-primary font-medium hover:underline flex items-center gap-1"
            >
              <CheckCheck size={12} /> Segna tutte come lette
            </button>
          </div>
        )}

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
            const cfg = TYPE_CONFIG[notification.type] || TYPE_CONFIG.SystemAnnouncement;
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
  );
}
