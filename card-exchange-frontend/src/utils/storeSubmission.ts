import type { StoreInput } from '../types';

/**
 * Stessi controlli del server (Core/Rules/StoreSubmissionRules.cs): tutti i campi obbligatori tranne Instagram.
 * Le chiavi sono quelle che il server restituisce in "missing" e "invalid".
 */
export type StoreField = 'name' | 'description' | 'address' | 'city' | 'province' | 'postalCode' | 'location'
  | 'openingHours' | 'phone' | 'website' | 'games';

export interface SubmissionProblems { missing: StoreField[]; invalid: StoreField[] }

const blank = (v?: string | null) => !v || !v.trim();

export function isPhone(phone: string) {
  const p = phone.trim();
  if (!/^\+?[\d\s.\-()/]+$/.test(p)) return false;
  const digits = p.replace(/\D/g, '').length;
  return digits >= 6 && digits <= 15;
}

export function isWebsite(website: string) {
  let w = website.trim();
  if (!/^https?:\/\//i.test(w)) w = `https://${w}`;
  try {
    const host = new URL(w).hostname;
    return host.includes('.') && !host.endsWith('.');
  } catch {
    return false;
  }
}

export function checkStoreSubmission(f: StoreInput): SubmissionProblems {
  const missing: StoreField[] = [];
  const invalid: StoreField[] = [];
  const required: [StoreField, string | null | undefined][] = [
    ['name', f.name], ['description', f.description], ['address', f.address], ['city', f.city],
    ['province', f.province], ['postalCode', f.postalCode],
  ];
  for (const [key, value] of required) if (blank(value)) missing.push(key);
  if (f.latitude === 0 && f.longitude === 0) missing.push('location');
  for (const [key, value] of [['openingHours', f.openingHours], ['phone', f.phone], ['website', f.website]] as [StoreField, string | null | undefined][])
    if (blank(value)) missing.push(key);
  if (f.gameIds.length === 0) missing.push('games');

  if (!missing.includes('name') && f.name.trim().length < 2) invalid.push('name');
  if (!missing.includes('description') && (f.description ?? '').trim().length < 20) invalid.push('description');
  if (!missing.includes('postalCode') && (f.country || 'IT').toUpperCase() === 'IT' && !/^\d{5}$/.test((f.postalCode ?? '').trim())) invalid.push('postalCode');
  if (!missing.includes('phone') && !isPhone(f.phone ?? '')) invalid.push('phone');
  if (!missing.includes('website') && !isWebsite(f.website ?? '')) invalid.push('website');
  return { missing, invalid };
}
