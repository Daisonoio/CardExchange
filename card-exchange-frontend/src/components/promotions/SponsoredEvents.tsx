import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { CalendarDays, Flag, MapPin } from 'lucide-react';
import { promotions, type SponsoredEvent } from '../../api';
import { formatEventDate } from '../../utils/eventDate';

/**
 * Eventi in evidenza vicino a chi guarda: sempre con l'etichetta "Sponsorizzato", segnalabili.
 * Sono visibili anche ai minorenni (solo qui, nella lista: nessuna notifica).
 */
export default function SponsoredEvents({ coords, className = '' }: { coords?: { latitude: number; longitude: number } | null; className?: string }) {
  const { t } = useTranslation('promotions');
  const navigate = useNavigate();
  const [items, setItems] = useState<SponsoredEvent[]>([]);
  const [reported, setReported] = useState<Set<number>>(new Set());
  const lat = coords?.latitude;
  const lng = coords?.longitude;

  useEffect(() => {
    promotions.featuredEvents(lat != null && lng != null ? { latitude: lat, longitude: lng } : null)
      .then(({ data }) => setItems(data)).catch(() => setItems([]));
  }, [lat, lng]);

  const visible = items.filter((e) => !reported.has(e.promotionId));
  if (visible.length === 0) return null;

  const open = (e: SponsoredEvent) => {
    promotions.click(e.promotionId).catch(() => undefined);
    navigate(`/events/${e.id}`);
  };
  const report = async (e: SponsoredEvent) => {
    if (!confirm(t('report.confirm'))) return;
    await promotions.report(e.promotionId).catch(() => undefined);
    setReported(new Set([...reported, e.promotionId]));
  };

  return (
    <ul className={`space-y-2 ${className}`} data-testid="sponsored-events">
      {visible.map((e) => (
        <li key={e.promotionId} className="relative bg-amber-50/70 border border-amber-200 rounded-2xl">
          <button type="button" onClick={() => open(e)} className="w-full text-left p-3 pr-10 flex gap-3">
            <span className="w-11 h-11 shrink-0 rounded-xl bg-white grid place-items-center text-amber-700"><CalendarDays size={20} /></span>
            <span className="min-w-0">
              <span className="block text-[10px] font-bold uppercase tracking-wide text-amber-800">{t('label')} · {e.sponsor}</span>
              <span className="block text-sm font-bold text-text truncate">{e.title}</span>
              <span className="block text-xs text-text-secondary truncate">
                {formatEventDate(e.startDate)} · <MapPin size={11} className="inline -mt-0.5" /> {e.city}
              </span>
            </span>
          </button>
          <button type="button" onClick={() => report(e)} aria-label={t('report.action')} title={t('report.action')}
            className="absolute right-2 top-2 p-1.5 rounded-lg text-text-muted hover:bg-white">
            <Flag size={14} />
          </button>
        </li>
      ))}
    </ul>
  );
}
