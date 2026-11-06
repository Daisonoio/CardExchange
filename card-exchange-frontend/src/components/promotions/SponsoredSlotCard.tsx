import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Info, Megaphone } from 'lucide-react';
import { sponsored, type SlotPlacementName, type SponsoredSlotView } from '../../api';

interface Props {
  placement: SlotPlacementName;
  coords?: { latitude: number; longitude: number } | null;
  gameId?: number | null;
  className?: string;
}

/**
 * Spazio sponsorizzato nativo: stessa forma delle schede vicine, etichetta "Sponsorizzato" e
 * "Perché vedo questo?" con zona e gioco usati. Si usa solo in Home ed Esplora, mai in chat o scambi
 * (una regola di ESLint lo impedisce).
 */
export default function SponsoredSlotCard({ placement, coords, gameId, className = '' }: Props) {
  const { t } = useTranslation('promotions');
  const navigate = useNavigate();
  const [slot, setSlot] = useState<SponsoredSlotView | null>(null);
  const [why, setWhy] = useState(false);
  const lat = coords?.latitude;
  const lon = coords?.longitude;

  useEffect(() => {
    let cancelled = false;
    sponsored.slot(placement, { lat, lon, gameId })
      .then(({ data }) => { if (!cancelled) setSlot(data || null); })
      .catch(() => { if (!cancelled) setSlot(null); });
    return () => { cancelled = true; };
  }, [placement, lat, lon, gameId]);

  if (!slot) return null;
  const open = () => {
    sponsored.click(slot.id).catch(() => undefined);
    // Solo pagine interne
    navigate(slot.targetUrl.startsWith('/') ? slot.targetUrl : '/');
  };

  return (
    <div className={`relative rounded-2xl border border-amber-200 bg-amber-50/60 ${className}`} data-testid="sponsored-slot">
      <button type="button" onClick={open} className="w-full flex items-center gap-3 p-3 pr-10 text-left">
        {slot.imageUrl
          ? <img src={slot.imageUrl} alt="" className="w-12 h-12 rounded-xl object-cover bg-white shrink-0" />
          : <span className="w-12 h-12 rounded-xl bg-white grid place-items-center text-amber-700 shrink-0"><Megaphone size={20} /></span>}
        <span className="min-w-0">
          <span className="block text-[10px] font-bold uppercase tracking-wide text-amber-800">{t('label')} · {slot.sponsor}</span>
          <span className="block text-sm font-bold text-text truncate">{slot.title}</span>
          {slot.body && <span className="block text-xs text-text-secondary line-clamp-2">{slot.body}</span>}
        </span>
      </button>
      <button type="button" onClick={() => setWhy(!why)} aria-expanded={why} aria-label={t('why.action')} title={t('why.action')}
        className="absolute right-2 top-2 p-1.5 rounded-lg text-text-muted hover:bg-white">
        <Info size={14} />
      </button>
      {why && (
        <p className="px-3 pb-3 -mt-1 text-[11px] text-text-secondary" role="note" data-testid="slot-why">
          {slot.why.zone === 'Zona' ? t('why.zone', { city: slot.why.city ?? '', km: slot.why.radiusKm }) : t('why.italy')}
          {' '}{slot.why.game ? t('why.game', { game: slot.why.game }) : t('why.anyGame')}
          {' '}{slot.brand ? t('why.brand') : t('why.local')}
        </p>
      )}
    </div>
  );
}
