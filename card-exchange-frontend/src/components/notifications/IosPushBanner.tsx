import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { BellRing, X } from 'lucide-react';
import { deviceInfo, iosTooOld } from '../../utils/push';
import BottomSheet from '../ui/BottomSheet';
import NotificationsGuide from './NotificationsGuide';

const DISMISS_KEY = 'ce.iosPushBanner.dismissedAt';
const SNOOZE_DAYS = 14;

function dismissedRecently(): boolean {
  try {
    const at = Number(localStorage.getItem(DISMISS_KEY));
    return !!at && Date.now() - at < SNOOZE_DAYS * 86_400_000;
  } catch {
    return false;
  }
}

/** Su iPhone, finché l'app non è aggiunta alla schermata Home, ricorda come ricevere gli avvisi. */
export default function IosPushBanner() {
  const { t } = useTranslation('notifications');
  const [hidden, setHidden] = useState(() => {
    const d = deviceInfo();
    return !d.ios || d.standalone || iosTooOld(d) || dismissedRecently();
  });
  const [open, setOpen] = useState(false);
  if (hidden) return null;

  const dismiss = () => {
    try { localStorage.setItem(DISMISS_KEY, String(Date.now())); } catch { /* solo per questa visita */ }
    setHidden(true);
  };

  return (
    <div className="bg-white rounded-2xl p-4 shadow-sm border border-border/50 mb-4">
      <div className="flex items-start gap-3">
        <span className="w-9 h-9 rounded-full bg-primary/10 text-primary flex items-center justify-center shrink-0"><BellRing size={16} /></span>
        <div className="flex-1 min-w-0">
          <h2 className="text-sm font-bold text-text">{t('guide.bannerTitle')}</h2>
          <p className="text-xs text-text-secondary mt-0.5">{t('guide.bannerText')}</p>
          <div className="mt-2 flex gap-4">
            <button type="button" onClick={() => setOpen(true)} className="text-sm font-semibold text-primary">{t('guide.cta')}</button>
            <button type="button" onClick={dismiss} className="text-sm text-text-muted">{t('guide.bannerDismiss')}</button>
          </div>
        </div>
        <button type="button" onClick={dismiss} aria-label={t('guide.bannerDismiss')} className="p-1 -m-1 text-text-muted"><X size={16} /></button>
      </div>
      <BottomSheet open={open} onClose={() => setOpen(false)} title={t('guide.title')}>
        <NotificationsGuide />
      </BottomSheet>
    </div>
  );
}
