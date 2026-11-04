import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import client from '../api/client';

/**
 * Traduzioni. Ogni file locales/<lingua>/<area>.json è un namespace caricato automaticamente:
 * per tradurre un componente si usa useTranslation('<area>') e t('chiave').
 * Lingua: scelta salvata ("lang" in localStorage) → lingua del browser → italiano.
 */
export const SUPPORTED_LANGUAGES = [
  { code: 'it', label: 'Italiano', locale: 'it-IT' },
  { code: 'en', label: 'English', locale: 'en-GB' },
] as const;
export type AppLanguage = (typeof SUPPORTED_LANGUAGES)[number]['code'];

const modules = import.meta.glob('./locales/*/*.json', { eager: true }) as Record<string, { default: Record<string, unknown> }>;
const resources: Record<string, Record<string, Record<string, unknown>>> = {};
for (const [path, mod] of Object.entries(modules)) {
  const match = /\.\/locales\/([^/]+)\/([^/]+)\.json$/.exec(path);
  if (!match) continue;
  const [, lng, ns] = match;
  (resources[lng] ??= {})[ns] = mod.default;
}

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: 'it',
    supportedLngs: SUPPORTED_LANGUAGES.map((l) => l.code),
    nonExplicitSupportedLngs: true, // en-US → en
    defaultNS: 'common',
    ns: Object.keys(resources.it ?? {}),
    interpolation: { escapeValue: false }, // React fa già l'escape
    detection: { order: ['localStorage', 'navigator'], lookupLocalStorage: 'lang', caches: ['localStorage'] },
    returnNull: false,
  });

function applyLanguage(lng: string) {
  const code = lng.split('-')[0];
  document.documentElement.lang = code;
  // Il server risponde nella stessa lingua quando può (messaggi d'errore, email)
  client.defaults.headers.common['Accept-Language'] = code;
}
applyLanguage(i18n.resolvedLanguage ?? 'it');
i18n.on('languageChanged', applyLanguage);

/** Locale Intl della lingua corrente (per numeri, valute e date). */
export function currentLocale(): string {
  const code = (i18n.resolvedLanguage ?? 'it').split('-')[0];
  return SUPPORTED_LANGUAGES.find((l) => l.code === code)?.locale ?? 'it-IT';
}

export default i18n;
