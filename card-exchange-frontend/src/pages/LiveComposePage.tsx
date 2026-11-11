import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { MapContainer, Marker, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import { ArrowLeft, Bell, Crosshair, Loader2, MapPin, Radio, Store as StoreIcon, CalendarDays, Megaphone } from 'lucide-react';
import { broadcasts, events, follows, stores, type LiveBroadcast } from '../api';
import { useAuth } from '../context/AuthContext';
import type { CardEvent, Store } from '../types';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';
import '../components/map/leafletSetup';
import { useEntitlements } from '../hooks/useEntitlements';

type Point = { lat: number; lng: number };
const DURATIONS = ['60', '120', '240', '480', 'eod'] as const;
type DurationKey = (typeof DURATIONS)[number];

/** Minuti fino a fine giornata (ora locale), tra 30 minuti e 12 ore come sul server. */
function minutesFor(key: DurationKey): number {
  if (key !== 'eod') return Number(key);
  const end = new Date();
  end.setHours(23, 59, 0, 0);
  return Math.min(720, Math.max(30, Math.round((end.getTime() - Date.now()) / 60000)));
}

function PickOnMap({ onPick }: { onPick: (p: Point) => void }) {
  useMapEvents({ click: (e) => onPick({ lat: e.latlng.lat, lng: e.latlng.lng }) });
  return null;
}

function FollowPoint({ point }: { point: Point }) {
  const map = useMap();
  useEffect(() => { map.setView([point.lat, point.lng], Math.max(map.getZoom(), 15)); }, [point, map]);
  return null;
}

/** "Fai sapere dove sei": avviso ai follower con posto, punto preciso e durata. */
export default function LiveComposePage() {
  const { t } = useTranslation('live');
  const { paidPlans } = useEntitlements();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [params] = useSearchParams();

  const [myStores, setMyStores] = useState<Store[]>([]);
  const [nearbyEvents, setNearbyEvents] = useState<CardEvent[]>([]);
  const [info, setInfo] = useState<{ remainingToday: number; maxPerDay: number; followers: number; active?: LiveBroadcast } | null>(null);
  const [storeFollowers, setStoreFollowers] = useState<number | null>(null);

  const [storeId, setStoreId] = useState<number | null>(params.get('storeId') ? Number(params.get('storeId')) : null);
  const [eventId, setEventId] = useState<number | null>(params.get('eventId') ? Number(params.get('eventId')) : null);
  const [placeName, setPlaceName] = useState('');
  const [spotDetail, setSpotDetail] = useState('');
  const [message, setMessage] = useState('');
  const [point, setPoint] = useState<Point | null>(null);
  const [duration, setDuration] = useState<DurationKey>('240');
  const [locating, setLocating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  useEffect(() => {
    stores.mine().then(({ data }) => setMyStores(data.filter((s) => s.status === 'Verified'))).catch(() => setMyStores([]));
    events.getNearby().then(({ data }) => setNearbyEvents(data)).catch(() => setNearbyEvents([]));
    broadcasts.mine().then(({ data }) => setInfo({
      remainingToday: data.remainingToday,
      maxPerDay: data.maxPerDay,
      followers: data.followers,
      active: data.items.find((b) => b.isLive),
    })).catch(() => setInfo(null));
  }, []);

  useEffect(() => {
    if (!storeId) { setStoreFollowers(null); return; }
    follows.storeStatus(storeId).then(({ data }) => setStoreFollowers(data.followers)).catch(() => setStoreFollowers(null));
  }, [storeId]);

  const store = myStores.find((s) => s.id === storeId) ?? null;
  const event = nearbyEvents.find((e) => e.id === eventId) ?? null;

  // Punto di partenza: evento, negozio o posizione del profilo
  const fallbackCenter: Point = useMemo(() => {
    if (event?.latitude != null && event?.longitude != null) return { lat: event.latitude, lng: event.longitude };
    if (store) return { lat: store.latitude, lng: store.longitude };
    const loc = user?.location;
    if (loc?.latitude && loc?.longitude) return { lat: Number(loc.latitude), lng: Number(loc.longitude) };
    return { lat: 41.9, lng: 12.5 };
  }, [event, store, user]);

  // Scegliendo un evento si propone il suo nome e il suo luogo
  const chooseEvent = (id: number | null) => {
    setEventId(id);
    const ev = nearbyEvents.find((e) => e.id === id);
    if (ev) {
      if (!placeName.trim()) setPlaceName(ev.title.slice(0, 100));
      if (ev.latitude != null && ev.longitude != null) setPoint({ lat: ev.latitude, lng: ev.longitude });
    }
  };

  const useGps = () => {
    if (!navigator.geolocation) { setError(t('compose.gpsError')); return; }
    setLocating(true);
    setError(null);
    navigator.geolocation.getCurrentPosition(
      (pos) => { setPoint({ lat: pos.coords.latitude, lng: pos.coords.longitude }); setLocating(false); },
      () => { setError(t('compose.gpsError')); setLocating(false); },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  };

  const authorName = store ? store.name : `@${user?.username ?? ''}`;
  const limitReached = info != null && info.remainingToday <= 0;
  const canSend = placeName.trim().length >= 2 && (point != null || store != null || (event?.latitude != null)) && !limitReached;
  const followersCount = store ? null : info?.followers ?? null;

  const send = async () => {
    if (!canSend) return;
    setSending(true);
    setError(null);
    try {
      const { data } = await broadcasts.create({
        storeId: storeId ?? undefined,
        eventId: eventId ?? undefined,
        placeName: placeName.trim(),
        spotDetail: spotDetail.trim() || undefined,
        message: message.trim() || undefined,
        latitude: point?.lat,
        longitude: point?.lng,
        durationMinutes: minutesFor(duration),
      });
      navigate(`/live/${data.id}`, { replace: true });
    } catch (err) {
      setError(apiErrorMessage(err, t('compose.error')));
    } finally {
      setSending(false);
    }
  };

  const input = 'w-full px-3 py-2.5 rounded-xl border border-border bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/30';
  const label = 'block text-xs font-semibold text-text-secondary mb-1';
  const shown = point ?? fallbackCenter;

  return (
    <div className="pb-24 max-w-xl space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0">
          <ArrowLeft size={18} />
        </button>
        <h1 className="text-xl font-bold text-text flex items-center gap-2"><Radio size={20} className="text-primary" /> {t('compose.title')}</h1>
      </div>
      <p className="text-sm text-text-secondary">{t('compose.intro')}</p>

      {info?.active && (
        <p className="text-xs p-3 rounded-xl bg-amber-50 text-amber-800">{t('compose.replaces')}</p>
      )}
      {info?.active && paidPlans && (
        <Link to={`/promotions?type=broadcast&target=${info.active.id}`} className="flex items-center justify-center gap-2 w-full py-2.5 rounded-xl border border-amber-300 bg-white text-sm font-semibold text-amber-800">
          <Megaphone size={15} /> {t('promotions:sponsorBroadcast')}
        </Link>
      )}

      <div className="bg-white rounded-2xl border border-border/60 p-4 space-y-4">
        {myStores.length > 0 && (
          <div>
            <span className={label}>{t('compose.as')}</span>
            <div className="flex flex-wrap gap-2">
              <button type="button" onClick={() => setStoreId(null)} aria-pressed={storeId == null}
                className={`px-3 py-1.5 rounded-full text-xs font-semibold border ${storeId == null ? 'bg-primary text-white border-primary' : 'bg-white text-text-secondary border-border'}`}>
                {t('compose.asMe', { username: user?.username })}
              </button>
              {myStores.map((s) => (
                <button key={s.id} type="button" onClick={() => { setStoreId(s.id); if (!point) setPoint({ lat: s.latitude, lng: s.longitude }); }} aria-pressed={storeId === s.id}
                  className={`inline-flex items-center gap-1 px-3 py-1.5 rounded-full text-xs font-semibold border ${storeId === s.id ? 'bg-primary text-white border-primary' : 'bg-white text-text-secondary border-border'}`}>
                  <StoreIcon size={12} /> {s.name}
                </button>
              ))}
            </div>
          </div>
        )}

        {nearbyEvents.length > 0 && (
          <div>
            <label htmlFor="live-event" className={label}>{t('compose.event')}</label>
            <div className="relative">
              <CalendarDays size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted pointer-events-none" />
              <select id="live-event" value={eventId ?? ''} onChange={(e) => chooseEvent(e.target.value ? Number(e.target.value) : null)} className={`${input} pl-8`}>
                <option value="">{t('compose.noEvent')}</option>
                {nearbyEvents.map((e) => <option key={e.id} value={e.id}>{e.title} · {e.city}</option>)}
              </select>
            </div>
          </div>
        )}

        <div>
          <label htmlFor="live-place" className={label}>{t('compose.place')}</label>
          <input id="live-place" value={placeName} onChange={(e) => setPlaceName(e.target.value)} maxLength={100} placeholder={t('compose.placePlaceholder')} className={input} />
        </div>

        <div>
          <label htmlFor="live-spot" className={label}>{t('compose.spot')}</label>
          <input id="live-spot" value={spotDetail} onChange={(e) => setSpotDetail(e.target.value)} maxLength={120} placeholder={t('compose.spotPlaceholder')} className={input} />
        </div>

        <div>
          <span className={label}>{t('compose.where')}</span>
          <div className="flex flex-wrap gap-2 mb-2">
            <button type="button" onClick={useGps} disabled={locating}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-primary/10 text-primary text-xs font-semibold hover:bg-primary/15 disabled:opacity-60">
              {locating ? <Loader2 size={14} className="animate-spin" /> : <Crosshair size={14} />}
              {locating ? t('compose.locating') : t('compose.useGps')}
            </button>
            {store && (
              <button type="button" onClick={() => setPoint({ lat: store.latitude, lng: store.longitude })}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-surface-dark text-text-secondary text-xs font-semibold hover:bg-border">
                <StoreIcon size={14} /> {t('compose.useStore')}
              </button>
            )}
            {event?.latitude != null && event?.longitude != null && (
              <button type="button" onClick={() => setPoint({ lat: event.latitude!, lng: event.longitude! })}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-surface-dark text-text-secondary text-xs font-semibold hover:bg-border">
                <CalendarDays size={14} /> {t('compose.useEvent')}
              </button>
            )}
          </div>
          <div className="h-48 rounded-xl overflow-hidden border border-border">
            <MapContainer center={[shown.lat, shown.lng]} zoom={point ? 15 : 12} style={{ height: '100%', width: '100%' }} scrollWheelZoom={false}>
              <TileLayer attribution="&copy; OpenStreetMap" url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
              <PickOnMap onPick={setPoint} />
              {point && <><Marker position={[point.lat, point.lng]} /><FollowPoint point={point} /></>}
            </MapContainer>
          </div>
          <p className="text-[11px] text-text-muted mt-1 flex items-center gap-1"><MapPin size={11} /> {t('compose.pickOnMap')}</p>
        </div>

        <div>
          <span className={label}>{t('compose.duration')}</span>
          <div className="flex flex-wrap gap-2">
            {DURATIONS.map((d) => (
              <button key={d} type="button" onClick={() => setDuration(d)} aria-pressed={duration === d}
                className={`px-3 py-1.5 rounded-full text-xs font-semibold border ${duration === d ? 'bg-primary text-white border-primary' : 'bg-white text-text-secondary border-border'}`}>
                {t(`compose.durations.${d}`)}
              </button>
            ))}
          </div>
        </div>

        <div>
          <label htmlFor="live-message" className={label}>{t('compose.message')}</label>
          <textarea id="live-message" value={message} onChange={(e) => setMessage(e.target.value)} maxLength={280} rows={2} placeholder={t('compose.messagePlaceholder')} className={`${input} resize-none`} />
          <p className="text-[10px] text-text-muted text-right">{message.length}/280</p>
        </div>
      </div>

      {/* Anteprima di quello che vedranno i follower */}
      <div>
        <p className="text-xs font-semibold text-text-secondary mb-1">{t('compose.preview')}</p>
        <div className="flex gap-3 p-3 rounded-2xl bg-white border border-border/60 shadow-sm">
          <div className="w-9 h-9 rounded-full bg-primary/10 text-primary flex items-center justify-center shrink-0"><Bell size={16} /></div>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-text truncate">{t('compose.previewTitle', { name: authorName })}</p>
            <p className="text-xs text-text-secondary line-clamp-2">
              {[placeName.trim() || t('compose.placePlaceholder'), spotDetail.trim()].filter(Boolean).join(' · ')}
              {message.trim() && <> — {message.trim()}</>}
            </p>
          </div>
        </div>
      </div>

      {info && (
        <p className={`text-xs ${limitReached ? 'text-red-600' : 'text-text-muted'}`}>
          {limitReached ? t('compose.limitReached', { max: info.maxPerDay }) : t('compose.remaining', { count: info.remainingToday, max: info.maxPerDay })}
        </p>
      )}
      {followersCount === 0 && storeFollowers == null && <p className="text-xs text-text-muted">{t('compose.noFollowersHint')}</p>}
      {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{error}</p>}

      <Button onClick={send} disabled={!canSend} isLoading={sending} className="w-full" size="lg">
        <Radio size={16} />
        {followersCount ? t('compose.send', { count: followersCount }) : t('compose.sendNoFollowers')}
      </Button>
    </div>
  );
}
