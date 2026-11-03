import { useEffect, useState } from 'react';
import { MapContainer, Marker, TileLayer, useMap } from 'react-leaflet';
import { Loader2, MapPin } from 'lucide-react';
import './leafletSetup';

export interface AddressValue {
  address: string;
  city: string;
  province?: string;
  postalCode?: string;
  latitude: number;
  longitude: number;
}

function Recenter({ lat, lng }: { lat: number; lng: number }) {
  const map = useMap();
  useEffect(() => { map.setView([lat, lng], 16); }, [lat, lng, map]);
  return null;
}

/**
 * Indirizzo + posizione esatta: si scrive l'indirizzo, "Trova sulla mappa" lo geolocalizza (Nominatim)
 * e il marker si può trascinare per correggere la posizione.
 */
export default function AddressPicker({ value, onChange }: { value: AddressValue; onChange: (v: AddressValue) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const hasPosition = value.latitude !== 0 || value.longitude !== 0;

  const geocode = async () => {
    setBusy(true);
    setError(null);
    try {
      const q = [value.address, value.postalCode, value.city, value.province, 'Italia'].filter(Boolean).join(', ');
      const res = await fetch(`https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(q)}&format=json&limit=1&addressdetails=1`, {
        headers: { 'Accept-Language': 'it' },
      });
      const data = await res.json();
      if (!data.length) {
        setError('Indirizzo non trovato: controllalo o sposta il marker a mano dopo aver cercato la città.');
        return;
      }
      const a = data[0].address ?? {};
      onChange({
        ...value,
        latitude: Number(data[0].lat),
        longitude: Number(data[0].lon),
        postalCode: value.postalCode || a.postcode,
        province: value.province || a.county?.replace(/^Provincia di /, ''),
      });
    } catch {
      setError('Ricerca non riuscita, riprova.');
    } finally {
      setBusy(false);
    }
  };

  const field = (key: keyof AddressValue, label: string, placeholder?: string, className = '') => (
    <label className={`block text-xs font-semibold text-text-secondary ${className}`}>
      {label}
      <input
        value={(value[key] as string) ?? ''}
        placeholder={placeholder}
        onChange={(e) => onChange({ ...value, [key]: e.target.value })}
        className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm font-normal text-text"
      />
    </label>
  );

  return (
    <div className="space-y-2">
      {field('address', 'Indirizzo', 'Via Roma 1')}
      <div className="grid grid-cols-3 gap-2">
        {field('city', 'Città', 'Milano', 'col-span-2')}
        {field('province', 'Provincia', 'MI')}
      </div>
      {field('postalCode', 'CAP', '20121')}
      <button
        type="button"
        onClick={geocode}
        disabled={busy || !value.address || !value.city}
        className="inline-flex items-center gap-1.5 text-xs font-semibold text-primary disabled:opacity-50"
      >
        {busy ? <Loader2 size={14} className="animate-spin" /> : <MapPin size={14} />} Trova sulla mappa
      </button>
      {error && <p className="text-xs text-danger">{error}</p>}
      {hasPosition && (
        <div className="space-y-1">
          <div className="h-48 rounded-xl overflow-hidden border border-border">
            <MapContainer center={[value.latitude, value.longitude]} zoom={16} style={{ height: '100%', width: '100%' }}>
              <TileLayer attribution='&copy; OpenStreetMap' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
              <Recenter lat={value.latitude} lng={value.longitude} />
              <Marker
                position={[value.latitude, value.longitude]}
                draggable
                eventHandlers={{
                  dragend: (e) => {
                    const p = e.target.getLatLng();
                    onChange({ ...value, latitude: p.lat, longitude: p.lng });
                  },
                }}
              />
            </MapContainer>
          </div>
          <p className="text-[10px] text-text-muted">Trascina il marker se la posizione non è precisa.</p>
        </div>
      )}
    </div>
  );
}
