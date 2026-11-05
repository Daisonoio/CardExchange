// Ricerca e geocodifica inversa con Nominatim (OpenStreetMap), limitata all'Italia.
export interface Place {
  label: string;
  city: string;
  province: string;
  country: string;
  postalCode?: string;
  latitude: number;
  longitude: number;
}

interface NominatimAddress {
  city?: string; town?: string; village?: string; municipality?: string; suburb?: string;
  county?: string; state?: string; postcode?: string; country_code?: string;
  'ISO3166-2-lvl6'?: string;
}
interface NominatimResult { lat: string; lon: string; display_name: string; address?: NominatimAddress }

const BASE = 'https://nominatim.openstreetmap.org';

function toPlace(r: NominatimResult): Place {
  const a = r.address ?? {};
  const city = a.city || a.town || a.village || a.municipality || a.suburb || r.display_name.split(',')[0];
  // "IT-MI" -> "MI"; in mancanza, il nome della provincia senza prefisso
  const province = a['ISO3166-2-lvl6']?.split('-')[1]
    || (a.county ?? '').replace(/^(Provincia di|Città Metropolitana di|Province of)\s+/i, '')
    || a.state || '';
  return {
    label: [city, province].filter(Boolean).join(', '),
    city,
    province,
    country: (a.country_code ?? 'it').toUpperCase(),
    postalCode: a.postcode,
    latitude: Number(r.lat),
    longitude: Number(r.lon),
  };
}

export async function searchPlaces(query: string, language = 'it'): Promise<Place[]> {
  if (query.trim().length < 2) return [];
  const res = await fetch(`${BASE}/search?q=${encodeURIComponent(query)}&format=json&limit=5&countrycodes=it&addressdetails=1`, {
    headers: { 'Accept-Language': language },
  });
  if (!res.ok) return [];
  const data = (await res.json()) as NominatimResult[];
  return data.map(toPlace);
}

export async function reversePlace(latitude: number, longitude: number, language = 'it'): Promise<Place | null> {
  const res = await fetch(`${BASE}/reverse?lat=${latitude}&lon=${longitude}&format=json&zoom=12&addressdetails=1`, {
    headers: { 'Accept-Language': language },
  });
  if (!res.ok) return null;
  const data = (await res.json()) as NominatimResult & { error?: string };
  if (data.error) return null;
  return { ...toPlace(data), latitude, longitude };
}
