import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, ArrowLeftRight, BarChart3, CalendarDays, Clock, Globe, Instagram, Loader2, MapPin, Phone, QrCode, Radio, ShieldCheck, Sparkles } from 'lucide-react';
import { events, stores } from '../api';
import type { CardEvent, Store } from '../types';
import { EVENT_TYPE_LABELS } from '../types';
import { formatEventDate } from '../utils/eventDate';
import { useGame } from '../context/GameContext';
import { useFormat } from '../i18n/format';
import StoresMap from '../components/stores/StoresMap';
import EmptyState from '../components/ui/EmptyState';
import FollowButton from '../components/social/FollowButton';

const STATUS_LABEL: Record<Store['status'], { textKey: string; cls: string }> = {
  Pending: { textKey: 'page.status.Pending', cls: 'bg-amber-50 text-amber-700' },
  Verified: { textKey: 'page.status.Verified', cls: 'bg-green-50 text-green-700' },
  Rejected: { textKey: 'page.status.Rejected', cls: 'bg-red-50 text-red-700' },
  Suspended: { textKey: 'page.status.Suspended', cls: 'bg-gray-100 text-gray-600' },
};

/** Scheda pubblica di un negozio. */
export default function StorePage() {
  const { t } = useTranslation('stores');
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { games } = useGame();
  const { dateTime } = useFormat();
  const [store, setStore] = useState<Store | null | undefined>(undefined);
  const [storeEvents, setStoreEvents] = useState<CardEvent[]>([]);
  const [attributions, setAttributions] = useState<{ total: number; last30Days: number } | null>(null);
  const [storeMeetups, setStoreMeetups] = useState<{ upcoming: { id: number; scheduledAt: string }[]; completedLast30Days: number } | null>(null);

  useEffect(() => {
    stores.get(Number(id)).then(({ data }) => setStore(data)).catch(() => setStore(null));
    events.byStore(Number(id)).then(({ data }) => setStoreEvents(data)).catch(() => setStoreEvents([]));
  }, [id]);

  const isOwner = !!store?.isOwner;
  useEffect(() => {
    if (!isOwner) return;
    stores.attributions(Number(id)).then(({ data }) => setAttributions(data)).catch(() => setAttributions(null));
    stores.meetups(Number(id)).then(({ data }) => setStoreMeetups(data)).catch(() => setStoreMeetups(null));
  }, [id, isOwner]);

  if (store === undefined) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  if (store === null) return <EmptyState icon={MapPin} title={t('page.notFoundTitle')} description={t('page.notFoundDescription')} />;

  const gameNames = games.filter((g) => store.gameIds.includes(g.id)).map((g) => g.name);
  const directions = `https://www.openstreetmap.org/directions?to=${store.latitude}%2C${store.longitude}`;

  return (
    <div className="pb-24 space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0">
          <ArrowLeft size={18} />
        </button>
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold text-text truncate">{store.name}</h1>
          {store.isFeatured && (
            <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-amber-700"><Sparkles size={12} /> {t('page.featured')}</span>
          )}
          <p className="text-sm text-text-secondary truncate">{store.city}{store.province ? ` (${store.province})` : ''}</p>
        </div>
        {store.isOwner && (
          <Link to={`/stores/${store.id}/edit`} className="text-xs font-semibold text-primary hover:underline shrink-0">{t('page.edit')}</Link>
        )}
        {!store.isOwner && store.status === 'Verified' && <FollowButton target={{ storeId: store.id }} showCount />}
      </div>

      {store.isOwner && store.status === 'Verified' && storeMeetups && (
        <div className="p-3 rounded-xl bg-white border border-border">
          <p className="text-sm font-semibold text-text flex items-center gap-1.5"><ArrowLeftRight size={16} className="text-primary" /> {t('page.meetups.title')}</p>
          {storeMeetups.upcoming.length === 0 ? (
            <p className="text-xs text-text-secondary mt-0.5">{t('page.meetups.none')}</p>
          ) : (
            <ul className="mt-1 space-y-0.5">
              {storeMeetups.upcoming.slice(0, 5).map((m) => (
                <li key={m.id} className="text-xs text-text-secondary">{dateTime(m.scheduledAt, { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })} · {t('page.meetups.people')}</li>
              ))}
            </ul>
          )}
          <p className="text-[11px] text-text-muted mt-1">{t('page.meetups.completed', { count: storeMeetups.completedLast30Days })}</p>
        </div>
      )}

      {store.isOwner && store.status === 'Verified' && (
        <Link to={`/stores/${store.id}/results`} className="flex items-center gap-3 p-3 rounded-xl bg-white border border-border hover:border-primary/40">
          <BarChart3 size={18} className="shrink-0 text-primary" />
          <span className="min-w-0">
            <strong className="block text-sm text-text">{t('results.entry')}</strong>
            <span className="text-xs text-text-secondary">{t('results.entryHint')}</span>
          </span>
        </Link>
      )}

      {store.isOwner && store.status === 'Verified' && (
        <Link to={`/qr/store/${store.id}`} className="flex items-center gap-3 p-3 rounded-xl bg-white border border-border hover:border-primary/40">
          <QrCode size={18} className="shrink-0 text-primary" />
          <span className="min-w-0">
            <strong className="block text-sm text-text">{t('qr:store.entry')}</strong>
            <span className="text-xs text-text-secondary">
              {attributions ? t('qr:store.registered', { count: attributions.total, recent: attributions.last30Days }) : t('qr:store.entryHint')}
            </span>
          </span>
        </Link>
      )}

      {store.isOwner && store.status === 'Verified' && (
        <Link to={`/live/new?storeId=${store.id}`} className="flex items-center gap-3 p-3 rounded-xl bg-primary text-white hover:bg-primary-light">
          <Radio size={18} className="shrink-0" />
          <span className="min-w-0">
            <strong className="block text-sm">{t('live:compose.title')}</strong>
            <span className="text-xs text-white/80">{t('live:follow.hint')}</span>
          </span>
        </Link>
      )}

      {(store.isOwner || store.status !== 'Verified') && (
        <div className={`p-3 rounded-xl text-sm ${STATUS_LABEL[store.status].cls}`}>
          <strong>{t(STATUS_LABEL[store.status].textKey)}</strong>
          {store.reviewNote && <span> · {store.reviewNote}</span>}
          {store.status === 'Pending' && <span className="block text-xs mt-0.5">{t('page.pendingNote')}</span>}
        </div>
      )}

      {store.isOwner && store.status === 'Verified' && !store.isFeatured && (
        <Link to="/premium?for=store" className="block p-3 rounded-xl bg-secondary/10 text-sm text-text hover:bg-secondary/20">
          <strong className="flex items-center gap-1"><Sparkles size={14} className="text-amber-700" /> {t('page.promoteTitle')}</strong>
          <span className="text-xs text-text-secondary">{t('page.promoteDescription')}</span>
        </Link>
      )}

      {store.isSafeMeetupSpot && (
        <div className="flex items-center gap-2 p-3 rounded-xl bg-green-50 text-green-800 text-sm">
          <ShieldCheck size={18} className="shrink-0" />
          {t('page.safeMeetupSpot')}
        </div>
      )}

      {store.description && <p className="text-sm text-text whitespace-pre-line">{store.description}</p>}

      <StoresMap items={[store]} />

      <ul className="bg-white rounded-2xl border border-border divide-y divide-border/60 text-sm">
        <li className="flex items-start gap-3 p-3">
          <MapPin size={16} className="mt-0.5 text-primary shrink-0" />
          <div className="flex-1">
            {store.address}, {store.postalCode ? `${store.postalCode} ` : ''}{store.city}
            <a href={directions} target="_blank" rel="noopener noreferrer" className="block text-xs font-semibold text-primary hover:underline">{t('page.directions')}</a>
          </div>
        </li>
        {store.openingHours && <li className="flex items-start gap-3 p-3"><Clock size={16} className="mt-0.5 text-primary shrink-0" />{store.openingHours}</li>}
        {store.phone && <li className="flex items-center gap-3 p-3"><Phone size={16} className="text-primary shrink-0" /><a href={`tel:${store.phone}`} className="hover:underline">{store.phone}</a></li>}
        {store.website && <li className="flex items-center gap-3 p-3"><Globe size={16} className="text-primary shrink-0" /><a href={store.website} target="_blank" rel="noopener noreferrer" className="hover:underline truncate">{store.website.replace(/^https?:\/\//, '').replace(/\/$/, '')}</a></li>}
        {store.instagram && <li className="flex items-center gap-3 p-3"><Instagram size={16} className="text-primary shrink-0" /><a href={`https://instagram.com/${store.instagram}`} target="_blank" rel="noopener noreferrer" className="hover:underline">@{store.instagram}</a></li>}
      </ul>

      {gameNames.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {gameNames.map((n) => <span key={n} className="px-2.5 py-1 rounded-full bg-primary/10 text-primary text-xs font-medium">{n}</span>)}
        </div>
      )}

      <section className="bg-white rounded-2xl border border-border p-3">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-semibold flex items-center gap-2"><CalendarDays size={16} className="text-primary" /> {t('page.upcomingEvents')}</h2>
          {store.isOwner && store.status === 'Verified' && (
            <Link to="/events/new" className="text-xs font-semibold text-primary hover:underline">{t('page.createEvent')}</Link>
          )}
        </div>
        {storeEvents.length === 0 ? (
          <p className="text-xs text-text-muted mt-1">{t('page.noEvents')}</p>
        ) : (
          <ul className="mt-2 space-y-1">
            {storeEvents.map((e) => (
              <li key={e.id}>
                <Link to={`/events/${e.id}`} className="block p-2 rounded-xl hover:bg-surface-dark">
                  <span className="block text-sm font-medium">{e.title}</span>
                  <span className="block text-xs text-text-muted">{EVENT_TYPE_LABELS[e.type] ?? e.type} · {formatEventDate(e.startDate)} · {t('page.participants', { count: e.participantCount })}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
