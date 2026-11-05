import { pushApi } from '../api';

export type PushState = 'unsupported' | 'disabled-server' | 'denied' | 'off' | 'on' | 'ios-needs-install' | 'ios-too-old';

const urlBase64ToUint8Array = (base64: string) => {
  const padding = '='.repeat((4 - (base64.length % 4)) % 4);
  const raw = atob((base64 + padding).replace(/-/g, '+').replace(/_/g, '/'));
  return Uint8Array.from(raw, (c) => c.charCodeAt(0));
};

export interface DeviceInfo {
  /** iPhone o iPad (anche iPad che si presenta come Mac). */
  ios: boolean;
  android: boolean;
  /** Aperta dall'icona sulla schermata Home. */
  standalone: boolean;
  /** Versione principale e minore di iOS, se nota. */
  iosVersion: [number, number] | null;
  /** Browser interno di un'app (Instagram, Facebook, TikTok…): da lì non si può aggiungere alla Home. */
  inAppBrowser: boolean;
  /** Su iPhone, browser diverso da Safari (Chrome, Firefox, Edge…). */
  otherIosBrowser: boolean;
}

/** Le notifiche web su iPhone esistono da iOS 16.4. */
export const IOS_MIN_PUSH: [number, number] = [16, 4];

export function deviceInfo(ua = navigator.userAgent): DeviceInfo {
  const ios = /iphone|ipad|ipod/i.test(ua) || (/macintosh/i.test(ua) && typeof navigator !== 'undefined' && navigator.maxTouchPoints > 1);
  const v = ua.match(/OS (\d+)_(\d+)/);
  return {
    ios,
    android: /android/i.test(ua),
    standalone: typeof window !== 'undefined'
      && (window.matchMedia?.('(display-mode: standalone)').matches || (navigator as unknown as { standalone?: boolean }).standalone === true),
    iosVersion: ios && v ? [Number(v[1]), Number(v[2])] : null,
    inAppBrowser: /FBAN|FBAV|Instagram|TikTok|musical_ly|Line\/|Snapchat|Twitter/i.test(ua),
    otherIosBrowser: ios && /CriOS|FxiOS|EdgiOS|OPiOS|GSA\//i.test(ua),
  };
}

export const iosTooOld = (d: DeviceInfo) =>
  !!d.iosVersion && (d.iosVersion[0] < IOS_MIN_PUSH[0] || (d.iosVersion[0] === IOS_MIN_PUSH[0] && d.iosVersion[1] < IOS_MIN_PUSH[1]));

/** Stato attuale delle notifiche push su questo dispositivo. */
export async function getPushState(): Promise<PushState> {
  const device = deviceInfo();
  if (device.ios && iosTooOld(device)) return 'ios-too-old';
  if (device.ios && !device.standalone) return 'ios-needs-install';
  if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) return 'unsupported';
  const { data } = await pushApi.publicKey();
  if (!data.enabled) return 'disabled-server';
  if (Notification.permission === 'denied') return 'denied';
  const registration = await navigator.serviceWorker.ready;
  const subscription = await registration.pushManager.getSubscription();
  return subscription ? 'on' : 'off';
}

/** Chiede il permesso, registra il dispositivo e lo comunica al server. */
export async function enablePush(): Promise<PushState> {
  const { data } = await pushApi.publicKey();
  if (!data.enabled || !data.publicKey) return 'disabled-server';
  const permission = await Notification.requestPermission();
  if (permission !== 'granted') return permission === 'denied' ? 'denied' : 'off';
  const registration = await navigator.serviceWorker.ready;
  const subscription = (await registration.pushManager.getSubscription())
    ?? await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: urlBase64ToUint8Array(data.publicKey) });
  const json = subscription.toJSON();
  await pushApi.subscribe({ endpoint: subscription.endpoint, keys: { p256dh: json.keys!.p256dh, auth: json.keys!.auth } });
  return 'on';
}

export async function disablePush(): Promise<PushState> {
  const registration = await navigator.serviceWorker.ready;
  const subscription = await registration.pushManager.getSubscription();
  if (subscription) {
    await pushApi.unsubscribe(subscription.endpoint).catch(() => {});
    await subscription.unsubscribe();
  }
  return 'off';
}
