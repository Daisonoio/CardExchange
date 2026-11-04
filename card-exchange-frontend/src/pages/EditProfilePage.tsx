import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Save, Navigation, MapPin, TrendingUp } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { users, priceTracking } from '../api';
import i18n from '../i18n';
import { useAuth } from '../context/AuthContext';
import { useGeolocation } from '../hooks/useGeolocation';
import Button from '../components/ui/Button';
import { RADIUS_MAX, RADIUS_MIN, RADIUS_STEP, resetSearchRadius } from '../hooks/useSearchRadius';

export default function EditProfilePage() {
  const { t } = useTranslation('profile');
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
      setMessage({ type: 'success', text: t('edit.spikeSaved') });
    } catch {
      setMessage({ type: 'error', text: t('edit.spikeSaveError') });
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
        `https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lon}&format=json&accept-language=${i18n.resolvedLanguage ?? 'it'}`
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
      setMessage({ type: 'success', text: t('edit.profileSaved') });
    } catch {
      setMessage({ type: 'error', text: t('edit.profileSaveError') });
    } finally {
      setIsSaving(false);
    }
  };

  const handleSaveLocation = async () => {
    if (!user) return;
    if (!locationForm.city || !locationForm.province || !locationForm.country) {
      setMessage({ type: 'error', text: t('edit.locationRequired') });
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
      // Il raggio salvato nel profilo diventa quello usato in tutte le sezioni
      resetSearchRadius(user.id);
      await refreshUser();
      setMessage({ type: 'success', text: t('edit.locationSaved') });
    } catch {
      setMessage({ type: 'error', text: t('edit.locationSaveError') });
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
        <h1 className="text-lg font-bold">{t('edit.title')}</h1>
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
        <h2 className="text-sm font-bold mb-4">{t('edit.personalInfo')}</h2>

        <div className="space-y-3">
          <div>
            <label className={labelClass}>{t('edit.firstName')}</label>
            <input
              type="text"
              className={inputClass}
              value={form.firstName}
              onChange={(e) => setForm({ ...form, firstName: e.target.value })}
            />
          </div>
          <div>
            <label className={labelClass}>{t('edit.lastName')}</label>
            <input
              type="text"
              className={inputClass}
              value={form.lastName}
              onChange={(e) => setForm({ ...form, lastName: e.target.value })}
            />
          </div>
          <div>
            <label className={labelClass}>{t('edit.bio')}</label>
            <textarea
              className={`${inputClass} resize-none`}
              rows={3}
              value={form.bio}
              onChange={(e) => setForm({ ...form, bio: e.target.value })}
              placeholder={t('edit.bioPlaceholder')}
            />
          </div>
          <div>
            <label className={labelClass}>{t('edit.paypal')}</label>
            <input
              type="text"
              className={inputClass}
              value={form.paypalUsername}
              onChange={(e) => setForm({ ...form, paypalUsername: e.target.value })}
              placeholder={t('edit.paypalPlaceholder')}
            />
          </div>
          <div>
            <label className={labelClass}>{t('edit.satispay')}</label>
            <input
              type="text"
              className={inputClass}
              value={form.satispayUsername}
              onChange={(e) => setForm({ ...form, satispayUsername: e.target.value })}
              placeholder={t('edit.satispayPlaceholder')}
            />
          </div>
          <div>
            <label className={labelClass}>{t('edit.paymentQr')}</label>
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
          <Save size={14} /> {t('edit.saveProfile')}
        </Button>
      </div>

      {/* Location section */}
      <div className="bg-white rounded-2xl p-5 shadow-sm border border-border/50">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-sm font-bold">{t('edit.location')}</h2>
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
          {isReverseGeocoding ? t('edit.detectingAddress') : t('edit.useCurrentPosition')}
        </Button>

        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={labelClass}>{t('edit.city')}</label>
              <input
                type="text"
                className={inputClass}
                value={locationForm.city}
                onChange={(e) => setLocationForm({ ...locationForm, city: e.target.value })}
                placeholder={t('edit.cityPlaceholder')}
              />
            </div>
            <div>
              <label className={labelClass}>{t('edit.province')}</label>
              <input
                type="text"
                className={inputClass}
                value={locationForm.province}
                onChange={(e) => setLocationForm({ ...locationForm, province: e.target.value })}
                placeholder={t('edit.provincePlaceholder')}
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={labelClass}>{t('edit.country')}</label>
              <input
                type="text"
                className={inputClass}
                value={locationForm.country}
                onChange={(e) => setLocationForm({ ...locationForm, country: e.target.value })}
                placeholder={t('edit.countryPlaceholder')}
              />
            </div>
            <div>
              <label className={labelClass}>{t('edit.postalCode')}</label>
              <input
                type="text"
                className={inputClass}
                value={locationForm.postalCode}
                onChange={(e) => setLocationForm({ ...locationForm, postalCode: e.target.value })}
                placeholder={t('edit.postalCodePlaceholder')}
              />
            </div>
          </div>

          {/* Max distance slider */}
          <div>
            <label className={labelClass}>
              {t('edit.searchRadius', { km: locationForm.maxDistanceKm })}
            </label>
            <input
              type="range"
              min={RADIUS_MIN}
              max={RADIUS_MAX}
              step={RADIUS_STEP}
              value={locationForm.maxDistanceKm}
              onChange={(e) =>
                setLocationForm({ ...locationForm, maxDistanceKm: parseInt(e.target.value) })
              }
              className="w-full accent-primary"
            />
          </div>
        </div>

        <Button onClick={handleSaveLocation} isLoading={isSavingLocation} className="w-full mt-4">
          <MapPin size={14} /> {t('edit.saveLocation')}
        </Button>
      </div>

      {/* Spike threshold section */}
      <div className="bg-white rounded-2xl p-5 shadow-sm border border-border/50 mt-4">
        <div className="flex items-center gap-2 mb-4">
          <TrendingUp size={16} className="text-amber-500" />
          <h2 className="text-sm font-bold">{t('edit.priceAlerts')}</h2>
        </div>
        <p className="text-xs text-text-muted mb-3">
          {t('edit.priceAlertsDescription')}
        </p>
        <div>
          <label className={labelClass}>
            {t('edit.spikeThreshold', { value: spikeThreshold })}
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
          <TrendingUp size={14} /> {t('edit.saveThreshold')}
        </Button>
      </div>
    </div>
  );
}
