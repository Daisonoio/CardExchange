import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle, BellRing, Check, Compass, Copy, Share, Smartphone, SquarePlus } from 'lucide-react';
import { deviceInfo, iosTooOld } from '../../utils/push';

/** Indirizzo pubblico della guida, da condividere (es. dai negozi). */
export const GUIDE_PATH = '/help/notifications';

const IOS_STEPS = [
  { key: 'safari', Icon: Compass },
  { key: 'share', Icon: Share },
  { key: 'add', Icon: SquarePlus },
  { key: 'open', Icon: BellRing },
] as const;

function CopyLink() {
  const { t } = useTranslation('notifications');
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(`${window.location.origin}${GUIDE_PATH}`);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };
  return (
    <button type="button" onClick={copy} className="mt-2 inline-flex items-center gap-1.5 text-sm font-semibold text-primary">
      {copied ? <Check size={14} /> : <Copy size={14} />} {copied ? t('guide.copied') : t('guide.copyLink')}
    </button>
  );
}

function Warning({ children }: { children: React.ReactNode }) {
  return (
    <div role="note" className="flex gap-2 p-3 rounded-xl bg-amber-50 text-amber-800 text-sm">
      <AlertTriangle size={16} className="shrink-0 mt-0.5" />
      <div>{children}</div>
    </div>
  );
}

/**
 * Guida per attivare le notifiche: su iPhone arrivano solo dall'app aggiunta alla schermata Home.
 * Mostra prima la parte che riguarda il dispositivo in uso.
 */
export default function NotificationsGuide({ showAll = false }: { showAll?: boolean }) {
  const { t } = useTranslation('notifications');
  const device = deviceInfo();
  const showIos = showAll || device.ios || !device.android;
  const showAndroid = showAll || device.android;

  const iosSection = showIos && (
    <section key="ios" aria-labelledby="guide-ios" className="space-y-3">
      <h2 id="guide-ios" className="text-sm font-bold text-text flex items-center gap-2"><Smartphone size={16} className="text-primary" /> {t('guide.iphone')}</h2>
      {device.ios && iosTooOld(device) && (
        <Warning>{t('guide.tooOld', { version: device.iosVersion!.join('.') })}</Warning>
      )}
      {device.inAppBrowser && <Warning>{t('guide.inApp')}<br /><CopyLink /></Warning>}
      {!device.inAppBrowser && device.otherIosBrowser && <Warning>{t('guide.otherBrowser')}<br /><CopyLink /></Warning>}
      <ol className="space-y-2">
        {IOS_STEPS.map(({ key, Icon }, i) => (
          <li key={key} className="flex gap-3 p-3 rounded-xl bg-white border border-border">
            <span className="relative shrink-0 w-10 h-10 rounded-xl bg-primary/10 text-primary flex items-center justify-center">
              <Icon size={20} aria-hidden />
              <span className="absolute -top-1.5 -left-1.5 w-5 h-5 rounded-full bg-primary text-white text-[11px] font-bold flex items-center justify-center">{i + 1}</span>
            </span>
            <span className="min-w-0">
              <strong className="block text-sm text-text">{t(`guide.steps.${key}.title`)}</strong>
              <span className="block text-xs text-text-secondary mt-0.5">{t(`guide.steps.${key}.text`)}</span>
            </span>
          </li>
        ))}
      </ol>
    </section>
  );

  const androidSection = showAndroid && (
    <section key="android" aria-labelledby="guide-android" className="space-y-2">
      <h2 id="guide-android" className="text-sm font-bold text-text flex items-center gap-2"><Smartphone size={16} className="text-primary" /> {t('guide.android')}</h2>
      <ol className="list-decimal pl-5 space-y-1 text-sm text-text-secondary">
        {(t('guide.androidSteps', { returnObjects: true }) as string[]).map((s) => <li key={s}>{s}</li>)}
      </ol>
    </section>
  );

  return (
    <div className="space-y-5">
      <p className="text-sm text-text-secondary">{t('guide.intro')}</p>
      {device.android ? [androidSection, iosSection] : [iosSection, androidSection]}
      <section aria-labelledby="guide-blocked" className="space-y-2">
        <h2 id="guide-blocked" className="text-sm font-bold text-text">{t('guide.blocked')}</h2>
        <ul className="space-y-1 text-sm text-text-secondary">
          <li>{t('guide.blockedSteps.iphone')}</li>
          <li>{t('guide.blockedSteps.android')}</li>
          <li>{t('guide.blockedSteps.desktop')}</li>
        </ul>
      </section>
      <p className="text-xs text-text-muted">{t('guide.done')}</p>
    </div>
  );
}
