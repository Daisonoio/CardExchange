import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { MapContainer, Marker, TileLayer } from 'react-leaflet';
import { ArrowLeft, CalendarDays, Clock, Layers, Loader2, MapPin, MessageSquare, Navigation, QrCode, Radio, Store as StoreIcon, Users } from 'lucide-react';
import { broadcasts, type LiveBroadcast } from '../api';
import { useFormat } from '../i18n/format';
import EmptyState from '../components/ui/EmptyState';
import FollowButton from '../components/social/FollowButton';
import '../components/map/leafletSetup';

/** Dettaglio di un avviso "Sono qui": dove, fino a quando, mappa e indicazioni. */
export default function LivePage() {
  const { t } = useTranslation('live');
  const { time } = useFormat();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [item, setItem] = useState<LiveBroadcast | null | undefined>(undefined);
  const [ending, setEnding] = useState(false);

  useEffect(() => {
    broadcasts.get(Number(id)).then(({ data }) => setItem(data)).catch(() => setItem(null));
  }, [id]);

  if (item === undefined) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  if (item === null) return <EmptyState icon={MapPin} title={t('page.notFound')} description={t('page.notFoundDescription')} />;

  const name = item.store ? item.store.name : `@${item.author.username}`;
  const directions = `https://www.google.com/maps/dir/?api=1&destination=${item.latitude},${item.longitude}`;

  const endNow = async () => {
    setEnding(true);
    try { setItem((await broadcasts.end(item.id)).data); } finally { setEnding(false); }
  };

  return (
    <div className="pb-24 space-y-4 max-w-xl">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0">
          <ArrowLeft size={18} />
        </button>
        <div className="flex-1 min-w-0">
          <p className="text-xs font-semibold uppercase tracking-wide text-text-muted flex items-center gap-1">
            <Radio size={12} /> {t('page.title')}
          </p>
          <h1 className="text-xl font-bold text-text truncate">{name}</h1>
        </div>
        {!item.isMine && (
          <FollowButton target={item.store ? { storeId: item.store.id } : { userId: item.author.id }} showCount />
        )}
      </div>

      <div className="bg-white rounded-2xl border border-border/60 shadow-sm overflow-hidden">
        <div className="h-52">
          <MapContainer center={[item.latitude, item.longitude]} zoom={15} style={{ height: '100%', width: '100%' }} scrollWheelZoom={false}>
            <TileLayer attribution="&copy; OpenStreetMap" url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
            <Marker position={[item.latitude, item.longitude]} />
          </MapContainer>
        </div>
        <div className="p-4 space-y-2">
          <span className={`inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-full ${item.isLive ? 'bg-red-50 text-red-600' : 'bg-surface-dark text-text-muted'}`}>
            {item.isLive && <span className="w-1.5 h-1.5 rounded-full bg-red-500" aria-hidden="true" />}
            {item.isLive ? t('page.live') : t('page.ended')}
          </span>
          <p className="text-lg font-bold text-text flex items-start gap-1.5">
            <MapPin size={18} className="text-primary mt-1 shrink-0" /> {item.placeName}
          </p>
          {item.spotDetail && <p className="text-sm text-text flex items-center gap-1.5"><Layers size={14} className="text-text-muted" /> {item.spotDetail}</p>}
          <p className="text-sm text-text-secondary flex items-center gap-1.5">
            <Clock size={14} className="text-text-muted" />
            {item.isLive ? t('page.until', { time: time(item.endsAt) }) : t('page.endedAt', { time: time(item.endsAt) })}
            {item.distanceKm != null && <> · {t('page.distance', { km: item.distanceKm })}</>}
          </p>
          {item.event && (
            <Link to={`/events/${item.event.id}`} className="text-sm text-primary font-medium flex items-center gap-1.5 hover:underline">
              <CalendarDays size={14} /> {t('page.event', { title: item.event.title })}
            </Link>
          )}
          {item.message && <p className="text-sm text-text bg-surface-dark rounded-xl p-3 whitespace-pre-line">{item.message}</p>}
          {item.isMine && item.recipientCount != null && (
            <p className="text-xs text-text-muted flex items-center gap-1"><Users size={12} /> {t('page.recipients', { count: item.recipientCount })}</p>
          )}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <a href={directions} target="_blank" rel="noopener noreferrer"
          className="flex items-center justify-center gap-1.5 py-3 rounded-xl bg-primary text-white text-sm font-semibold hover:bg-primary-light">
          <Navigation size={16} /> {t('page.directions')}
        </a>
        {item.store ? (
          <Link to={`/stores/${item.store.id}`} className="flex items-center justify-center gap-1.5 py-3 rounded-xl bg-white border border-border text-sm font-semibold text-text hover:border-primary/40">
            <StoreIcon size={16} /> {t('page.store')}
          </Link>
        ) : !item.isMine ? (
          <Link to={`/collection/${item.author.id}`} className="flex items-center justify-center gap-1.5 py-3 rounded-xl bg-white border border-border text-sm font-semibold text-text hover:border-primary/40">
            <Layers size={16} /> {t('page.collection')}
          </Link>
        ) : null}
        {!item.isMine && (
          <Link to={`/chat?userId=${item.author.id}`} className="col-span-2 flex items-center justify-center gap-1.5 py-3 rounded-xl bg-white border border-border text-sm font-semibold text-text hover:border-primary/40">
            <MessageSquare size={16} /> {t('page.message')}
          </Link>
        )}
        {item.isMine && item.isLive && (
          <Link to={`/qr/live/${item.id}`} className="flex items-center justify-center gap-1.5 py-3 rounded-xl bg-white border border-border text-sm font-semibold text-text hover:border-primary/40">
            <QrCode size={16} /> {t('qr:live.entry')}
          </Link>
        )}
        {item.isMine && item.isLive && (
          <button onClick={endNow} disabled={ending} className="flex items-center justify-center gap-1.5 py-3 rounded-xl bg-white border border-red-200 text-sm font-semibold text-red-600 hover:bg-red-50 disabled:opacity-60">
            {ending ? t('page.ending') : t('page.endNow')}
          </button>
        )}
      </div>
    </div>
  );
}
