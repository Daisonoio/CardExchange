import { MapContainer, Marker, Popup, TileLayer } from 'react-leaflet';
import { Link } from 'react-router-dom';
import type { Store } from '../../types';
import '../map/leafletSetup';

/** Mappa dei negozi; centrata sul primo risultato (il più vicino). */
export default function StoresMap({ items, center }: { items: Store[]; center?: [number, number] }) {
  const start: [number, number] = center ?? (items[0] ? [items[0].latitude, items[0].longitude] : [41.9, 12.5]);
  return (
    <div className="h-56 rounded-2xl overflow-hidden border border-border">
      <MapContainer center={start} zoom={items.length ? 12 : 5} style={{ height: '100%', width: '100%' }} scrollWheelZoom={false}>
        <TileLayer attribution='&copy; OpenStreetMap' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
        {items.map((s) => (
          <Marker key={s.id} position={[s.latitude, s.longitude]}>
            <Popup>
              <Link to={`/stores/${s.id}`} className="font-semibold">{s.name}</Link>
              <br />
              {s.address}, {s.city}
            </Popup>
          </Marker>
        ))}
      </MapContainer>
    </div>
  );
}
