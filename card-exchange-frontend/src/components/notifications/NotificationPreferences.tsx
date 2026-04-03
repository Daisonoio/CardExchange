import { useState, useEffect } from 'react';
import { Loader2 } from 'lucide-react';
import { notifications } from '../../api';
import type { NotificationPreference } from '../../types';
import { notificationRegistry, NOTIFICATION_CATEGORIES } from './notificationRegistry';
import BottomSheet from '../ui/BottomSheet';

interface NotificationPreferencesProps {
  open: boolean;
  onClose: () => void;
}

export default function NotificationPreferences({ open, onClose }: NotificationPreferencesProps) {
  const [preferences, setPreferences] = useState<NotificationPreference[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setIsLoading(true);
    notifications.getPreferences()
      .then(({ data }) => {
        const list = data?.preferences ?? (data as any)?.Preferences ?? [];
        setPreferences(Array.isArray(list) ? list : []);
      })
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, [open]);

  const handleToggle = (typeId: number) => {
    setPreferences((prev) =>
      prev.map((p) => (p.typeId === typeId ? { ...p, isEnabled: !p.isEnabled } : p))
    );
  };

  const handleSave = async () => {
    setIsSaving(true);
    try {
      await notifications.updatePreferences(
        preferences.map((p) => ({ typeId: p.typeId, isEnabled: p.isEnabled }))
      );
      onClose();
    } catch {
    } finally {
      setIsSaving(false);
    }
  };

  const grouped = Object.entries(NOTIFICATION_CATEGORIES).map(([categoryKey, categoryLabel]) => ({
    key: categoryKey,
    label: categoryLabel,
    items: preferences.filter((p) => {
      const config = notificationRegistry[p.type];
      return config?.category === categoryKey;
    }),
  })).filter((g) => g.items.length > 0);

  return (
    <BottomSheet open={open} onClose={onClose} title="Preferenze notifiche">
      <div className="space-y-4">
        {isLoading ? (
          <div className="flex justify-center py-8">
            <Loader2 size={24} className="animate-spin text-text-muted" />
          </div>
        ) : (
          <>
            {grouped.map((group) => (
              <div key={group.key}>
                <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-2">
                  {group.label}
                </h3>
                <div className="space-y-1">
                  {group.items.map((pref) => {
                    const config = notificationRegistry[pref.type];
                    if (!config) return null;
                    const Icon = config.icon;
                    return (
                      <label
                        key={pref.typeId}
                        className="flex items-center gap-3 p-2.5 rounded-xl hover:bg-gray-50 cursor-pointer transition-colors"
                      >
                        <div className={`w-7 h-7 rounded-full flex items-center justify-center shrink-0 ${config.color}`}>
                          <Icon size={13} />
                        </div>
                        <span className="flex-1 text-sm text-text">{config.label}</span>
                        <div
                          role="switch"
                          aria-checked={pref.isEnabled}
                          onClick={() => handleToggle(pref.typeId)}
                          className={`relative w-10 h-5 rounded-full transition-colors cursor-pointer ${
                            pref.isEnabled ? 'bg-primary' : 'bg-gray-300'
                          }`}
                        >
                          <div
                            className={`absolute top-0.5 w-4 h-4 rounded-full bg-white shadow transition-transform ${
                              pref.isEnabled ? 'translate-x-5' : 'translate-x-0.5'
                            }`}
                          />
                        </div>
                      </label>
                    );
                  })}
                </div>
              </div>
            ))}

            <button
              onClick={handleSave}
              disabled={isSaving}
              className="w-full py-2.5 rounded-xl bg-primary text-white font-medium text-sm hover:bg-primary/90 disabled:opacity-50 flex items-center justify-center gap-2"
            >
              {isSaving && <Loader2 size={14} className="animate-spin" />}
              Salva preferenze
            </button>
          </>
        )}
      </div>
    </BottomSheet>
  );
}
