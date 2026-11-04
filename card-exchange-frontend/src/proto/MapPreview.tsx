import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Circle, MapContainer, Marker, TileLayer } from 'react-leaflet';
import L from 'leaflet';
import '../components/map/leafletSetup';
import { Loader2, MapPin } from 'lucide-react';
import { useFormat } from '../i18n/format';
import { apiErrorMessage } from '../utils/apiError';
import { explore, type ExploreArea, type ExploreResponse } from './api';
import { ThemeToggle } from './parts';

const RADII = [5, 10, 25, 50];

/** Bolla con il numero di carte nella zona (zone di circa 2 km: mai la posizione esatta di qualcuno). */
function areaIcon(count: number, active: boolean) {
  const size = count > 99 ? 46 : count > 9 ? 40 : 34;
  return L.divIcon({
    className: '',
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
    html: `<div style="width:${size}px;height:${size}px;border-radius:999px;display:flex;align-items:center;justify-content:center;
      font:800 13px 'Plus Jakarta Sans',system-ui;background:${active ? '#141029' : '#c8f53c'};color:${active ? '#c8f53c' : '#141029'};
      border:3px solid ${active ? '#c8f53c' : 'rgba(20,16,41,0.85)'};box-shadow:0 6px 16px rgba(0,0,0,0.35)">${count}</div>`,
  });
}

/** Anteprima: mappa delle carte vicine, raggruppate per zona. */
export default function MapPreview() {
  const { t } = useTranslation('proto');
  const { euro } = useFormat();
  const [radiusKm, setRadiusKm] = useState(10);
  const [data, setData] = useState<ExploreResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<ExploreArea | null>(null);

  useEffect(() => {
    let cancelled = false;
    explore.cards({ radiusKm, sort: 'distance', pageSize: 1 })
      .then(({ data: res }) => { if (!cancelled) { setData(res); setSelected(res.areas[0] ?? null); } })
      .catch((err) => { if (!cancelled) setError(apiErrorMessage(err, t('errors.load'))); });
    return () => { cancelled = true; };
  }, [radiusKm, t]);

  const center = useMemo<[number, number] | null>(() => (data?.origin ? [data.origin.lat, data.origin.lng] : null), [data]);
  const zoom = radiusKm <= 5 ? 13 : radiusKm <= 10 ? 12 : radiusKm <= 25 ? 11 : 10;

  return (
    <div className="px-4 pt-4">
      <div className="flex items-center gap-2 mb-3">
        <h1 className="text-2xl font-extrabold flex-1">{t('map.title')}</h1>
        <ThemeToggle />
      </div>
      <div className="flex gap-2 mb-3">
        {RADII.map((r) => (
          <button key={r} className="cx-chip px-3.5 py-1.5 text-sm font-semibold" aria-pressed={radiusKm === r} onClick={() => setRadiusKm(r)}>{r} km</button>
        ))}
      </div>

      {error && <div className="cx-card p-3 text-sm mb-3" style={{ color: 'var(--cx-danger)' }}>{error}</div>}
      {data && !data.hasLocation ? (
        <Link to="/profile/edit" className="cx-card p-4 text-sm flex items-center gap-2"><MapPin size={16} className="cx-accent-text" />{t('explore.setLocation')}</Link>
      ) : !center ? (
        <div className="cx-card h-[420px] flex items-center justify-center"><Loader2 className="animate-spin cx-accent-text" /></div>
      ) : (
        <div className="cx-map relative rounded-[24px] overflow-hidden" style={{ border: '1px solid var(--cx-border)' }}>
          <MapContainer key={`${center.join(',')}-${zoom}`} center={center} zoom={zoom} style={{ height: 440 }} zoomControl={false} attributionControl>
            <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" attribution="&copy; OpenStreetMap" />
            <Circle center={center} radius={radiusKm * 1000} pathOptions={{ color: '#8fc01a', weight: 1.5, fillColor: '#c8f53c', fillOpacity: 0.08 }} />
            <Circle center={center} radius={250} pathOptions={{ color: '#5b8cff', weight: 3, fillColor: '#5b8cff', fillOpacity: 0.9 }} />
            {data?.areas.map((a) => (
              <Marker key={`${a.lat},${a.lng}`} position={[a.lat, a.lng]} icon={areaIcon(a.count, selected === a)}
                eventHandlers={{ click: () => setSelected(a) }} />
            ))}
          </MapContainer>
          <span className="absolute left-3 top-3 z-[400] cx-glass rounded-full px-3 py-1.5 text-xs font-semibold">{t('map.count', { count: data?.total ?? 0 })}</span>
        </div>
      )}

      {selected && (
        <div className="cx-card mt-3 p-3">
          <p className="text-sm font-bold mb-2">
            {t('map.area', { count: selected.count })}
            {selected.approxKm != null && <span className="cx-muted font-semibold"> · {t('distance', { km: selected.approxKm })}</span>}
          </p>
          <div className="flex gap-3 overflow-x-auto" style={{ scrollbarWidth: 'none' }}>
            {selected.preview.map((p) => (
              <Link key={p.id} to={`/preview/card/${p.id}`} className="shrink-0 w-24">
                <div className="aspect-[63/88] rounded-xl overflow-hidden" style={{ background: 'var(--cx-surface-2)' }}>
                  {p.imageUrl && <img src={p.imageUrl} alt="" loading="lazy" className="w-full h-full object-cover" />}
                </div>
                <p className="text-[11px] font-bold mt-1 line-clamp-1">{p.name}</p>
                {p.price != null && <p className="text-[11px] cx-muted">{euro(p.price)}</p>}
              </Link>
            ))}
          </div>
        </div>
      )}
      <p className="text-[11px] cx-muted text-center mt-3 px-4">{t('map.privacy')}</p>
    </div>
  );
}
