import { useState, useEffect, useRef, useCallback } from 'react';
import { MapContainer, TileLayer, Marker, Circle, useMap, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import { Search, MapPin, Loader2 } from 'lucide-react';
import 'leaflet/dist/leaflet.css';

// Fix default marker icon (leaflet CSS issue with bundlers)
delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon-2x.png',
  iconUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon.png',
  shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png',
});

interface LocationPickerProps {
  initialLat?: number;
  initialLng?: number;
  initialRadius?: number;
  onLocationChange: (lat: number, lng: number, radius: number, label: string) => void;
}

interface NominatimResult {
  place_id: number;
  display_name: string;
  lat: string;
  lon: string;
}

/* Moves map center when position changes */
function RecenterMap({ lat, lng }: { lat: number; lng: number }) {
  const map = useMap();
  useEffect(() => {
    map.flyTo([lat, lng], Math.max(map.getZoom(), 12), {
      duration: 0.6,
      easeLinearity: 0.25,
    });
  }, [lat, lng, map]);
  return null;
}

/* Handles click on map to reposition marker */
function MapClickHandler({ onClick }: { onClick: (lat: number, lng: number) => void }) {
  useMapEvents({
    click(e) {
      onClick(e.latlng.lat, e.latlng.lng);
    },
  });
  return null;
}

export default function LocationPicker({
  initialLat = 45.0703,
  initialLng = 7.6869,
  initialRadius = 20,
  onLocationChange,
}: LocationPickerProps) {
  const [lat, setLat] = useState(initialLat);
  const [lng, setLng] = useState(initialLng);
  const [radius, setRadius] = useState(initialRadius);
  const [label, setLabel] = useState('');
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<NominatimResult[]>([]);
  const [searching, setSearching] = useState(false);
  const [showResults, setShowResults] = useState(false);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const wrapperRef = useRef<HTMLDivElement>(null);

  // Close dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setShowResults(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  // Notify parent on changes
  useEffect(() => {
    onLocationChange(lat, lng, radius, label);
  }, [lat, lng, radius, label, onLocationChange]);

  // Reverse geocode to get label when user clicks map
  const reverseGeocode = useCallback(async (latitude: number, longitude: number) => {
    try {
      const res = await fetch(
        `https://nominatim.openstreetmap.org/reverse?lat=${latitude}&lon=${longitude}&format=json&zoom=12&addressdetails=1`,
        { headers: { 'Accept-Language': 'it' } }
      );
      const data = await res.json();
      const addr = data.address;
      const parts = [addr?.city || addr?.town || addr?.village, addr?.state].filter(Boolean);
      setLabel(parts.join(', ') || data.display_name?.split(',').slice(0, 2).join(',') || '');
    } catch {
      setLabel('');
    }
  }, []);

  // Search via Nominatim
  const searchLocation = useCallback(async (q: string) => {
    if (q.length < 2) {
      setResults([]);
      return;
    }
    setSearching(true);
    try {
      const res = await fetch(
        `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(q)}&format=json&limit=5&countrycodes=it&addressdetails=1`,
        { headers: { 'Accept-Language': 'it' } }
      );
      const data: NominatimResult[] = await res.json();
      setResults(data);
      setShowResults(true);
    } catch {
      setResults([]);
    } finally {
      setSearching(false);
    }
  }, []);

  // Debounced search — shows dropdown suggestions while typing
  const handleQueryChange = (value: string) => {
    setQuery(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => searchLocation(value), 400);
  };

  const selectResult = (result: NominatimResult) => {
    const newLat = parseFloat(result.lat);
    const newLng = parseFloat(result.lon);
    setLat(newLat);
    setLng(newLng);
    const shortName = result.display_name.split(',').slice(0, 2).join(',').trim();
    setLabel(shortName);
    setQuery(shortName);
    setShowResults(false);
    setResults([]);
  };

  // Explicit search — triggered by button or Enter key; auto-selects first result
  const handleExplicitSearch = useCallback(async () => {
    if (query.length < 2) return;
    setSearching(true);
    setShowResults(false);
    try {
      const res = await fetch(
        `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(query)}&format=json&limit=5&countrycodes=it&addressdetails=1`,
        { headers: { 'Accept-Language': 'it' } }
      );
      const data: NominatimResult[] = await res.json();
      if (data.length > 0) {
        selectResult(data[0]);
      } else {
        setResults([]);
        setShowResults(true);
      }
    } catch {
      // ignore
    } finally {
      setSearching(false);
    }
  }, [query]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      if (debounceRef.current) clearTimeout(debounceRef.current);
      handleExplicitSearch();
    }
  };

  const handleMapClick = (newLat: number, newLng: number) => {
    setLat(newLat);
    setLng(newLng);
    reverseGeocode(newLat, newLng);
  };

  return (
    <div className="space-y-3">
      {/* Search bar */}
      <div ref={wrapperRef} className="relative">
        <div className="flex gap-2">
          <div className="relative flex-1">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
            <input
              type="text"
              value={query}
              onChange={(e) => handleQueryChange(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Cerca una città (es. Pavia, Milano...)"
              className="w-full pl-9 pr-10 py-2.5 rounded-xl border border-border text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
            />
            {searching && (
              <Loader2 size={16} className="absolute right-3 top-1/2 -translate-y-1/2 animate-spin text-primary" />
            )}
          </div>
          <button
            type="button"
            onClick={handleExplicitSearch}
            disabled={query.length < 2 || searching}
            className="flex items-center justify-center px-3 py-2.5 rounded-xl bg-primary text-white font-semibold text-sm disabled:opacity-40 transition-opacity shrink-0"
          >
            {searching ? <Loader2 size={16} className="animate-spin" /> : <Search size={16} />}
          </button>
        </div>

        {/* Results dropdown */}
        {showResults && results.length > 0 && (
          <div className="absolute z-[100] w-full mt-1 bg-white border border-border rounded-xl shadow-lg overflow-hidden">
            {results.map((r) => (
              <button
                key={r.place_id}
                onClick={() => selectResult(r)}
                className="w-full flex items-start gap-2 px-3 py-2.5 text-left hover:bg-primary/5 transition-colors border-b border-border/30 last:border-0"
              >
                <MapPin size={14} className="text-primary mt-0.5 shrink-0" />
                <span className="text-sm text-text leading-tight">
                  {r.display_name}
                </span>
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Map */}
      <div className="rounded-xl overflow-hidden border border-border h-[260px] md:h-[320px]">
        <MapContainer
          center={[lat, lng]}
          zoom={11}
          style={{ height: '100%', width: '100%' }}
          zoomControl={false}
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <Marker key={`${lat}-${lng}`} position={[lat, lng]} />
          <Circle
            center={[lat, lng]}
            radius={radius * 1000}
            pathOptions={{
              color: 'var(--color-primary, #6366f1)',
              fillColor: 'var(--color-primary, #6366f1)',
              fillOpacity: 0.1,
              weight: 2,
            }}
          />
          <RecenterMap lat={lat} lng={lng} />
          <MapClickHandler onClick={handleMapClick} />
        </MapContainer>
      </div>

      {/* Radius slider */}
      <div>
        <div className="flex items-center justify-between mb-1">
          <label className="text-xs font-semibold text-text-secondary">Raggio di ricerca</label>
          <span className="text-xs font-bold text-primary">{radius} km</span>
        </div>
        <input
          type="range"
          min={5}
          max={200}
          step={5}
          value={radius}
          onChange={(e) => setRadius(Number(e.target.value))}
          className="w-full accent-primary"
        />
        <div className="flex justify-between text-[10px] text-text-muted">
          <span>5 km</span>
          <span>200 km</span>
        </div>
      </div>

      {/* Selected location info */}
      {label && (
        <div className="flex items-center gap-2 px-3 py-2 bg-primary/5 rounded-xl">
          <MapPin size={14} className="text-primary shrink-0" />
          <span className="text-sm text-text font-medium">{label}</span>
          <span className="text-xs text-text-muted ml-auto">
            {lat.toFixed(4)}, {lng.toFixed(4)}
          </span>
        </div>
      )}
    </div>
  );
}
