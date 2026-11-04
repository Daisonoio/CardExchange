import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { BellRing } from 'lucide-react';
import { pushApi } from '../../api';
import { disablePush, enablePush, getPushState } from '../../utils/push';
import type { PushState } from '../../utils/push';

const DESCRIPTION_KEYS: Record<PushState, string> = {
  on: 'push.descriptions.on',
  off: 'push.descriptions.off',
  denied: 'push.descriptions.denied',
  unsupported: 'push.descriptions.unsupported',
  'disabled-server': 'push.descriptions.disabledServer',
  'ios-needs-install': 'push.descriptions.iosNeedsInstall',
};

/** Interruttore per le notifiche push su questo dispositivo. */
export default function PushToggle() {
  const { t } = useTranslation('notifications');
  const [state, setState] = useState<PushState | null>(null);
  const [busy, setBusy] = useState(false);
  const [info, setInfo] = useState<string | null>(null);

  useEffect(() => {
    getPushState().then(setState).catch(() => setState('unsupported'));
  }, []);

  if (state === null) return null;
  const canToggle = state === 'on' || state === 'off';

  const toggle = async () => {
    setBusy(true);
    setInfo(null);
    try {
      setState(state === 'on' ? await disablePush() : await enablePush());
    } catch {
      setInfo(t('push.toggleError'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-border/50 p-4 mb-4">
      <div className="flex items-center gap-3">
        <span className="w-9 h-9 rounded-full border-2 border-border flex items-center justify-center shrink-0" style={{ color: '#1a3461' }}>
          <BellRing size={16} />
        </span>
        <div className="flex-1">
          <p className="text-sm font-medium text-text">{t('push.title')}</p>
          <p className="text-xs text-text-muted">{t(DESCRIPTION_KEYS[state])}</p>
        </div>
        {canToggle && (
          <button
            type="button"
            role="switch"
            aria-checked={state === 'on'}
            aria-label={t('push.ariaLabel')}
            disabled={busy}
            onClick={toggle}
            className={`w-12 h-7 rounded-full transition-colors relative shrink-0 disabled:opacity-60 ${state === 'on' ? 'bg-primary' : 'bg-gray-300'}`}
          >
            <span className={`w-5 h-5 bg-white rounded-full absolute top-1 transition-transform ${state === 'on' ? 'translate-x-6' : 'translate-x-1'}`} />
          </button>
        )}
      </div>
      {state === 'on' && (
        <button
          type="button"
          onClick={() => pushApi.test().then(() => setInfo(t('push.testSent')))}
          className="mt-2 text-xs font-semibold text-primary hover:underline"
        >
          {t('push.sendTest')}
        </button>
      )}
      {info && <p className="mt-1 text-xs text-text-secondary">{info}</p>}
    </div>
  );
}
