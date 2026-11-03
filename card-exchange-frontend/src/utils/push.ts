import { pushApi } from '../api';

export type PushState = 'unsupported' | 'disabled-server' | 'denied' | 'off' | 'on' | 'ios-needs-install';

const urlBase64ToUint8Array = (base64: string) => {
  const padding = '='.repeat((4 - (base64.length % 4)) % 4);
  const raw = atob((base64 + padding).replace(/-/g, '+').replace(/_/g, '/'));
  return Uint8Array.from(raw, (c) => c.charCodeAt(0));
};

const isIos = () => /iphone|ipad|ipod/i.test(navigator.userAgent);
const isStandalone = () =>
  window.matchMedia('(display-mode: standalone)').matches || (navigator as unknown as { standalone?: boolean }).standalone === true;

/** Stato attuale delle notifiche push su questo dispositivo. */
export async function getPushState(): Promise<PushState> {
  if (isIos() && !isStandalone()) return 'ios-needs-install';
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
