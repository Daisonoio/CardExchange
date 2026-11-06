import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Check, ExternalLink, Loader2, QrCode, Store } from 'lucide-react';
import { showcase as showcaseApi, type MyShowcase, type ShowcaseStats } from '../api';
import { apiErrorMessage } from '../utils/apiError';
import Gate from '../components/ui/Gate';
import Button from '../components/ui/Button';

const MAX_FEATURED = 12;

/** Area Banco: modifica della vetrina, carte in evidenza, statistiche della settimana, QR per il banco. */
export default function MyShowcasePage() {
  const { t } = useTranslation('showcase');
  const [data, setData] = useState<MyShowcase | null>(null);
  const [form, setForm] = useState({ slug: '', displayName: '', bio: '', coverImageUrl: '' });
  const [featured, setFeatured] = useState<number[]>([]);
  const [stats, setStats] = useState<ShowcaseStats | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    showcaseApi.mine().then(({ data }) => {
      setData(data);
      setForm({ slug: data.slug, displayName: data.displayName, bio: data.bio ?? '', coverImageUrl: data.coverImageUrl ?? '' });
      setFeatured(data.featuredCardIds);
      if (data.hasPlan) showcaseApi.stats().then(({ data: s }) => setStats(s)).catch(() => setStats(null));
    }).catch(() => setData(null));
  }, []);

  const toggle = (id: number) => setFeatured((f) => f.includes(id) ? f.filter((x) => x !== id) : f.length >= MAX_FEATURED ? f : [...f, id]);

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true); setError(null); setSaved(false);
    try {
      await showcaseApi.save({ ...form, bio: form.bio || undefined, coverImageUrl: form.coverImageUrl || undefined, featuredCardIds: featured });
      setSaved(true);
      setData((d) => d && { ...d, exists: true, slug: form.slug });
    } catch (err) {
      setError(apiErrorMessage(err, t('saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  if (!data) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  const input = 'mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm';
  const fewer = t('fewer');

  return (
    <div className="pb-24 space-y-4 max-w-2xl">
      <div className="flex items-center gap-2">
        <Link to="/profile" className="p-1.5 -ml-1.5 rounded-lg hover:bg-surface-hover" aria-label={t('common:actions.back')}><ArrowLeft size={20} /></Link>
        <h1 className="text-xl font-bold text-text flex items-center gap-2"><Store size={20} className="text-primary" /> {t('myTitle')}</h1>
      </div>
      <p className="text-sm text-text-secondary">{t('myIntro')}</p>

      <Gate capability="SellerShowcase">
        {data.exists && (
          <div className="grid grid-cols-2 gap-2">
            <Link to={`/v/${data.slug}`} className="flex items-center justify-center gap-1.5 py-2.5 rounded-xl bg-white border border-border text-sm font-semibold text-primary">
              <ExternalLink size={15} /> {t('view')}
            </Link>
            <Link to="/qr/showcase" className="flex items-center justify-center gap-1.5 py-2.5 rounded-xl bg-white border border-border text-sm font-semibold text-primary">
              <QrCode size={15} /> {t('qr')}
            </Link>
          </div>
        )}

        {stats && (
          <section className="bg-white rounded-2xl border border-border/60 p-4">
            <h2 className="text-sm font-bold text-text mb-2">{t('stats.title')}</h2>
            <dl className="grid grid-cols-3 gap-2 text-center">
              {([['visits', stats.visits, true], ['savedCards', stats.savedCards, true], ['contacts', stats.contacts, true],
                ['broadcasts', stats.broadcasts, false], ['reached', stats.reached, true], ['followers', stats.followers, false]] as const).map(([k, v, aggregate]) => (
                <div key={k} className="p-2 rounded-xl bg-surface-dark">
                  <dt className="text-[10px] text-text-muted">{t(`stats.${k}`)}</dt>
                  <dd className="text-base font-bold tabular-nums">{aggregate && v === 0 ? fewer : v}</dd>
                </div>
              ))}
            </dl>
          </section>
        )}

        <form onSubmit={save} className="bg-white rounded-2xl border border-border/60 p-4 space-y-3">
          <label className="block">
            <span className="text-xs font-semibold text-text-secondary">{t('form.name')}</span>
            <input className={input} value={form.displayName} maxLength={60} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
          </label>
          <label className="block">
            <span className="text-xs font-semibold text-text-secondary">{t('form.slug')}</span>
            <div className="flex items-center gap-1 mt-1">
              <span className="text-sm text-text-muted">/v/</span>
              <input className="flex-1 px-3 py-2 rounded-xl border border-border text-sm" value={form.slug} maxLength={40}
                onChange={(e) => setForm({ ...form, slug: e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '') })} />
            </div>
          </label>
          <label className="block">
            <span className="text-xs font-semibold text-text-secondary">{t('form.bio')}</span>
            <textarea className={input} rows={3} maxLength={500} value={form.bio} placeholder={t('form.bioPlaceholder')}
              onChange={(e) => setForm({ ...form, bio: e.target.value })} />
          </label>
          <label className="block">
            <span className="text-xs font-semibold text-text-secondary">{t('form.cover')}</span>
            <input className={input} type="url" value={form.coverImageUrl} placeholder="https://…" onChange={(e) => setForm({ ...form, coverImageUrl: e.target.value })} />
          </label>

          <div>
            <span className="text-xs font-semibold text-text-secondary">{t('form.featured', { count: featured.length, max: MAX_FEATURED })}</span>
            {data.cards.length === 0 ? (
              <p className="text-xs text-text-muted mt-1">{t('form.noCards')}</p>
            ) : (
              <ul className="mt-2 grid grid-cols-4 sm:grid-cols-6 gap-2 max-h-80 overflow-y-auto">
                {data.cards.map((c) => {
                  const on = featured.includes(c.id);
                  return (
                    <li key={c.id}>
                      <button type="button" onClick={() => toggle(c.id)} aria-pressed={on} title={c.name}
                        className={`relative w-full aspect-[63/88] rounded-lg overflow-hidden border-2 ${on ? 'border-primary' : 'border-transparent'}`}>
                        {c.imageUrl ? <img src={c.imageUrl} alt={c.name} loading="lazy" className="w-full h-full object-cover" /> : <span className="text-[9px] p-1">{c.name}</span>}
                        {on && <span className="absolute top-1 right-1 w-5 h-5 rounded-full bg-primary text-white grid place-items-center"><Check size={12} /></span>}
                      </button>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>

          <p className="text-[11px] text-text-muted">{t('form.rules')}</p>
          {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{error}</p>}
          {saved && <p role="status" className="text-sm text-green-700">{t('saved')}</p>}
          <Button type="submit" isLoading={saving}>{t('save')}</Button>
        </form>
      </Gate>
    </div>
  );
}
