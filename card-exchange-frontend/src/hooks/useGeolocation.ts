import { useState, useCallback } from 'react';
import i18n from '../i18n';

interface GeoPosition {
  latitude: number;
  longitude: number;
}

export function useGeolocation() {
  const [position, setPosition] = useState<GeoPosition | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const requestPosition = useCallback(() => {
    if (!navigator.geolocation) {
      setError(i18n.t('layout:geolocation.unsupported'));
      return;
    }

    setIsLoading(true);
    setError(null);

    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setPosition({
          latitude: pos.coords.latitude,
          longitude: pos.coords.longitude,
        });
        setIsLoading(false);
      },
      (err) => {
        setError(
          err.code === 1
            ? i18n.t('layout:geolocation.denied')
            : i18n.t('layout:geolocation.unavailable')
        );
        setIsLoading(false);
      },
      { enableHighAccuracy: true, timeout: 10000 }
    );
  }, []);

  return { position, error, isLoading, requestPosition };
}
