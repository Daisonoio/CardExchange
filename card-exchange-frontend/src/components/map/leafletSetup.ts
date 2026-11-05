import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png';
import markerIcon from 'leaflet/dist/images/marker-icon.png';
import markerShadow from 'leaflet/dist/images/marker-shadow.png';

// Icona predefinita dei marker: con i bundler Leaflet non trova le immagini da solo
delete (L.Icon.Default.prototype as unknown as { _getIconUrl?: unknown })._getIconUrl;
// Icone servite dal nostro dominio (nessuna richiesta a CDN esterne)
L.Icon.Default.mergeOptions({
  iconRetinaUrl: markerIcon2x,
  iconUrl: markerIcon,
  shadowUrl: markerShadow,
});
