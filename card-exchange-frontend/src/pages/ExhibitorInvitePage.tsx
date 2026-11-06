import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Loader2, Radio, Ticket } from 'lucide-react';
import { eventPackages } from '../api';
import { apiErrorMessage } from '../utils/apiError';
import { formatEventDate } from '../utils/eventDate';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';

type Invite = Awaited<ReturnType<typeof eventPackages.invite>>['data'];

/** Invito a esporre: si accetta a nome proprio o di un proprio negozio, poi si pubblica il "Sono qui" con lo stand. */
export default function ExhibitorInvitePage() {
  const { t } = useTranslation('organizers');
  const { token } = useParams<{ token: string }>();
  const [invite, setInvite] = useState<Invite | null | undefined>(undefined);
  const [storeId, setStoreId] = useState<number | undefined>(undefined);
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!token) return;
    eventPackages.invite(token).then(({ data }) => { setInvite(data); setDone(data.mine && data.accepted); }).catch(() => setInvite(null));
  }, [token]);

  if (invite === undefined) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  if (invite === null) return <EmptyState icon={Ticket} title={t('invite.invalid')} />;

  const accept = async () => {
    setBusy(true); setError(null);
    try { await eventPackages.accept(token!, storeId); setDone(true); }
    catch (err) { setError(apiErrorMessage(err, t('error'))); }
    finally { setBusy(false); }
  };

  return (
    <div className="pb-24 max-w-md mx-auto space-y-4">
      <div className="bg-white rounded-2xl border border-border/60 p-5 space-y-2">
        <span className="text-[11px] font-bold uppercase tracking-wide text-primary">{t('invite.kicker')}</span>
        <h1 className="text-xl font-bold text-text">{invite.eventTitle}</h1>
        <p className="text-sm text-text-secondary">{formatEventDate(invite.startDate)} · {invite.city}</p>
        <p className="text-sm">{t('invite.booth', { booth: invite.boothCode, name: invite.name })}</p>
        {invite.description && <p className="text-xs text-text-secondary">{invite.description}</p>}
      </div>
      {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{error}</p>}
      {done ? (
        <div className="space-y-2">
          <p role="status" className="text-sm p-3 rounded-xl bg-green-50 text-green-800">{t('invite.done')}</p>
          <Link to={`/live/new?eventId=${invite.eventId}`} className="flex items-center justify-center gap-2 w-full py-2.5 rounded-xl bg-primary text-white text-sm font-semibold">
            <Radio size={15} /> {t('invite.postHere')}
          </Link>
          <Link to={`/events/${invite.eventId}`} className="block text-center text-sm text-primary font-semibold">{t('invite.openEvent')}</Link>
        </div>
      ) : invite.accepted ? (
        <p className="text-sm text-text-muted">{t('invite.used')}</p>
      ) : (
        <div className="space-y-2">
          {invite.stores.length > 0 && (
            <label className="block">
              <span className="text-xs font-semibold text-text-secondary">{t('invite.as')}</span>
              <select className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm" value={storeId ?? ''} onChange={(e) => setStoreId(e.target.value ? Number(e.target.value) : undefined)}>
                <option value="">{t('invite.asMe')}</option>
                {invite.stores.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </label>
          )}
          <Button className="w-full" isLoading={busy} onClick={accept}>{t('invite.accept')}</Button>
          <p className="text-[11px] text-text-muted">{t('invite.note')}</p>
        </div>
      )}
    </div>
  );
}
