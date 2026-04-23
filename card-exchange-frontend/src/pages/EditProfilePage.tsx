import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Save, Navigation, MapPin, TrendingUp } from 'lucide-react';
import { users, priceTracking } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGeolocation } from '../hooks/useGeolocation';
import Button from '../components/ui/Button';

export default function EditProfilePage() {
  const { user, refreshUser } = useAuth();
  const navigate = useNavigate();
  const { position, isLoading: geoLoading, requestPosition } = useGeolocation();

  const [form, setForm] = useState({
    firstName: '',
    lastName: '',
    bio: '',
    paypalUsername: '',
    satispayUsername: '',
    paymentQrCodeUrl: '',
  });

  const [locationForm, setLocationForm] = useState({
    city: '',
    province: '',
    country: '',
    postalCode: '',
    latitude: undefined as number | undefined,
    longitude: undefined as number | undefined,
    maxDistanceKm: 50,
  });

  const [isSaving, setIsSaving] = useState(false);
  const [isSavingLocation, setIsSavingLocation] = useState(false);
  const [isReverseGeocoding, setIsReverseGeocoding] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // Spike threshold settings
  const [spikeThreshold, setSpikeThreshold] = useState(10);
  const [isSavingSpike, setIsSavingSpike] = useState(false);

  useEffect(() => {
    if (!user) return;
    setForm({
      firstName: user.firstName || '',
      lastName: user.lastName || '',
      bio: user.bio || '',
      paypalUsername: user.paypalUsername || '',
      satispayUsername: user.satispayUsername || '',
      paymentQrCodeUrl: user.paymentQrCodeUrl || '',
    });
    if (user.location) {
      setLocationForm({
        city: user.location.city || '',
        province: user.location.province || '',
        country: user.location.country || '',
        postalCode: user.location.postalCode || '',
        latitude: user.location.latitude,
        longitude: user.location.longitude,
        maxDistanceKm: user.location.maxDistanceKm || 50,
      });
    }
  }, [user]);

  // Load spike threshold
  useEffect(() => {
    priceTracking.getSpikeSettings().then(({ data }) => {
      setSpikeThreshold(data.thresholdPercentage);
    }).catch(() => {});
  }, []);

  const handleSaveSpikeSettings = async () => {
    setIsSavingSpike(true);
    setMessage(null);
    try {
      await priceTracking.updateSpikeSettings(spikeThreshold);
      setMessage({ type: 'success', text: 'Soglia spike aggiornata con successo' });
    } catch {
      setMessage({ type: 'error', text: 'Errore durante il salvataggio della soglia' });
    } finally {
      setIsSavingSpike(false);
    }
  };

  // When geolocation resolves, do reverse geocoding
  useEffect(() => {
    if (!position) return;
    reverseGeocode(position.latitude, position.longitude);
  }, [position]);

  const reverseGeocode = async (lat: number, lon: number) => {
    setIsReverseGeocoding(true);
    try {
      const res = await fetch(
        `https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lon}&format=json&accept-language=it`
      );
      const data = await res.json();
      const addr = data.address || {};
      setLocationForm((prev) => ({
        ...prev,
        latitude: lat,
        longitude: lon,
        city: addr.city || addr.town || addr.village || addr.municipality || '',
        province: addr.county || addr.state || '',
        country: addr.country || '',
        postalCode: addr.postcode || '',
      }));
    } catch {
      // Fallback: set coordinates only
      setLocationForm((prev) => ({
        ...prev,
        latitude: lat,
        longitude: lon,
      }));
    } finally {
      setIsReverseGeocoding(false);
    }
  };

  const handleSaveProfile = async () => {
    if (!user) return;
    setIsSaving(true);
    setMessage(null);
    try {
      await users.update(user.id, form);
      await refreshUser();
      setMessage({ type: 'success', text: 'Profilo aggiornato con successo' });
    } catch {
      setMessage({ type: 'error', text: 'Errore durante il salvataggio del profilo' });
    } finally {
      setIsSaving(false);
    }
  };

  const handleSaveLocation = async () => {
    if (!user) return;
    if (!locationForm.city || !locationForm.province || !locationForm.country) {
      setMessage({ type: 'error', text: 'Compila almeno città, provincia e paese' });
      return;
    }
    setIsSavingLocation(true);
    setMessage(null);
    try {
      await users.updateLocation(user.id, {
        city: locationForm.city,
        province: locationForm.province,
        country: locationForm.country,
        postalCode: locationForm.postalCode || undefined,
        latitude: locationForm.latitude,
        longitude: locationForm.longitude,
        maxDistanceKm: locationForm.maxDistanceKm,
      });
      await refreshUser();
      setMessage({ type: 'success', text: 'Posizione aggiornata con successo' });
    } catch {
      setMessage({ type: 'error', text: 'Errore durante il salvataggio della posizione' });
    } finally {
      setIsSavingLocation(false);
    }
  };

  const handleUseCurrentPosition = () => {
    requestPosition();
  };

  if (!user) return null;

  const inputClass =
    'w-full px-3 py-2.5 rounded-xl border border-border bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/30';
  const labelClass = 'block text-xs font-semibold text-text-secondary mb-1';

  return (
    <div className="max-w-lg mx-auto pb-8">
      {/* Header */}
      <div className="flex items-center gap-3 mb-5">
        <button onClick={() => navigate('/profile')} className="p-1.5 hover:bg-surface-dark rounded-lg transition">
          <ArrowLeft size={20} />
        </button>
        <h1 className="text-lg font-bold">Modifica profilo</h1>
      </div>

      {/* Feedback message */}
      {message && (
        <div
          className={`mb-4 px-4 py-2.5 rounded-xl text-sm font-medium ${
            message.type === 'success'
              ? 'bg-green-50 text-green-700 border border-green-200'
              : 'bg-red-50 text-red-700 border border-red-200'
          }`}
        >
          {message.text}
        </div>
      )}

      {/* Profile section */}
      <div className="bg-white rounded-2xl p-5 shadow-sm border border-border/50 mb-4">
        <h2 className="text-sm font-bold mb-4">Informazioni personali</h2>

        <div className="space-y-3">
          <div>
            <label className={labelClass}>Nome</label>
            <input
              type="text"
              className={inputClass}
              value={form.firstName}
              onChange={(e) => setForm({ ...form, firstName: e.target.value })}
            />
          </div>
          <div>
            <label className={labelClass}>Cognome</label>
            <input
              type="text"
              className={inputClass}
              value={form.lastName}
              onChange={(e) => setForm({ ...form, lastName: e.target.value })}
            />
          </div>
          <div>
            <label className={labelClass}>Bio</label>
            <textarea
              className={`${inputClass} resize-none`}
              rows={3}
              value={form.bio}
              onChange={(e) => setForm({ ...form, bio: e.target.value })}
              placeholder="Racconta qualcosa di te..."
            />
          </div>
          <div>
            <label className={labelClass}>PayPal username (opzionale)</label>
            <input
              type="text"
              className={inputClass}
              value={form.paypalUsername}
              onChange={(e) => setForm({ ...form, paypalUsername: e.target.value })}
              placeholder="es. mario.rossi"
            />
          </div>
          <div>
            <label className={labelClass}>Satispay username (opzionale)</label>
            <input
              type="text"
              className={inputClass}
              value={form.satispayUsername}
              onChange={(e) => setForm({ ...form, satispayUsername: e.target.value })}
              placeholder="es. @mario.rossi"
            />
          </div>
          <div>
            <label className={labelClass}>URL QR pagamento (opzionale)</label>
            <input
              type="url"
              className={inputClass}
              value={form.paymentQrCodeUrl}
              onChange={(e) => setForm({ ...form, paymentQrCodeUrl: e.target.value })}
              placeholder="https://..."
            />
          </div>
        </div>

        <Button onClick={handleSaveProfile} isLoading={isSaving} className="w-full mt-4">
          <Save size={14} /> Salva profilo
        </Button>
      </div>

      {/* Location section */}
      <div className="bg-white rounded-2xl p-5 shadow-sm border border-border/50">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-sm font-bold">Posizione</h2>
          {locationForm.latitude && locationForm.longitude && (
            <span className="text-xs text-text-muted">
              {locationForm.latitude.toFixed(4)}, {locationForm.longitude.toFixed(4)}
            </span>
          )}
        </div>

        {/* Use current position button */}
        <Button
          onClick={handleUseCurrentPosition}
          variant="outline"
          className="w-full mb-4"
          isLoading={geoLoading || isReverseGeocoding}
        >
          <Navigation size={14} />
          {isReverseGeocoding ? 'Rilevamento indirizzo...' : 'Usa posizione attuale'}
        </Button>

        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={labelClass}>Città *</label>
              <input
                type="text"
                className={inputClass}
                value={locationForm.city}
                onChange={(e) => setLocationForm({ ...locationForm, city: e.target.value })}
                placeholder="es. Roma"
              />
            </div>
            <div>
              <label className={labelClass}>Provincia *</label>
              <input
                type="text"
                className={inputClass}
                value={locationForm.province}
                onChange={(e) => setLocationForm({ ...locationForm, province: e.target.value })}
                placeholder="es. RM"
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={labelClass}>Paese *</label>
              <input
                type="text"
                className={inputClass}
                value={locationForm.country}
                onChange={(e) => setLocationForm({ ...locationForm, country: e.target.value })}
                placeholder="es. Italia"
              />
            </div>
            <div>
              <label className={labelClass}>CAP</label>
              <input
                type="text"
                className={inputClass}
                value={locationForm.postalCode}
                onChange={(e) => setLocationForm({ ...locationForm, postalCode: e.target.value })}
                placeholder="es. 00100"
              />
            </div>
          </div>

          {/* Max distance slider */}
          <div>
            <label className={labelClass}>
              Raggio di ricerca: {locationForm.maxDistanceKm} km
            </label>
            <input
              type="range"
              min={5}
              max={200}
              step={5}
              value={locationForm.maxDistanceKm}
              onChange={(e) =>
                setLocationForm({ ...locationForm, maxDistanceKm: parseInt(e.target.value) })
              }
              className="w-full accent-primary"
            />
            <div className="flex justify-between text-xs text-text-muted">
              <span>5 km</span>
              <span>200 km</span>
            </div>
          </div>
        </div>

        <Button onClick={handleSaveLocation} isLoading={isSavingLocation} className="w-full mt-4">
          <MapPin size={14} /> Salva posizione
        </Button>
      </div>

      {/* Spike threshold section */}
      <div className="bg-white rounded-2xl p-5 shadow-sm border border-border/50 mt-4">
        <div className="flex items-center gap-2 mb-4">
          <TrendingUp size={16} className="text-amber-500" />
          <h2 className="text-sm font-bold">Avvisi Prezzo</h2>
        </div>
        <p className="text-xs text-text-muted mb-3">
          Ricevi una notifica quando le carte nella tua collezione aumentano di prezzo oltre la soglia impostata negli ultimi 5 giorni.
        </p>
        <div>
          <label className={labelClass}>
            Soglia spike: {spikeThreshold}%
          </label>
          <input
            type="range"
            min={1}
            max={50}
            step={1}
            value={spikeThreshold}
            onChange={(e) => setSpikeThreshold(parseInt(e.target.value))}
            className="w-full accent-amber-500"
          />
          <div className="flex justify-between text-xs text-text-muted">
            <span>1%</span>
            <span>50%</span>
          </div>
        </div>
        <Button onClick={handleSaveSpikeSettings} isLoading={isSavingSpike} className="w-full mt-4">
          <TrendingUp size={14} /> Salva soglia
        </Button>
      </div>
    </div>
  );
}
