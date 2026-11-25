import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Loader2, Search, ShieldAlert, Store, Users, X } from 'lucide-react';
import { adminUsers, type AdminUserDetail, type AdminUserRow, type ProfileTypeName } from '../api';
import { formatDate, formatDateTime } from '../i18n/format';
import { apiErrorMessage } from '../utils/apiError';
import { hasPermission } from '../utils/permissions';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';

const PROFILES: ProfileTypeName[] = ['Collector', 'Seller', 'Store', 'Organizer'];
const input = 'w-full px-3 py-2 rounded-xl border border-border text-sm bg-surface-card';

/** Scheda di un utente: tipo di profilo, fascia d'età, sicurezza, negozi, registro delle modifiche. */
function UserDetail({ id, onChanged, onClose }: { id: number; onChanged: () => void; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const [user, setUser] = useState<AdminUserDetail | null>(null);
  const [profile, setProfile] = useState<ProfileTypeName>('Collector');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);
  const canBan = hasPermission('USERS.BAN');

  const load = useCallback(() => {
    adminUsers.get(id).then(({ data }) => { setUser(data); setProfile(data.profileType); }).catch(() => setUser(null));
  }, [id]);
  useEffect(() => { load(); }, [load]);

  const run = async (fn: () => Promise<{ data: { message: string } }>) => {
    setBusy(true); setMessage(null);
    try {
      const { data } = await fn();
      setMessage({ ok: true, text: data.message });
      setNote('');
      load();
      onChanged();
    } catch (err) {
      setMessage({ ok: false, text: apiErrorMessage(err, t('users.error')) });
    } finally {
      setBusy(false);
    }
  };

  if (!user) return <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>;
  const suspended = !!user.chatSuspendedUntil;

  return (
    <section className="bg-surface-card rounded-2xl border border-border/60 p-4 space-y-4" data-testid="admin-user-detail">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <h2 className="text-lg font-bold text-text truncate">@{user.username}</h2>
          <p className="text-xs text-text-secondary break-all">{user.email}{user.city ? ` · ${user.city}` : ''}</p>
          <p className="text-[11px] text-text-muted">
            {t('users.joined', { date: formatDate(user.createdAt) })}{user.lastLoginAt ? ` · ${t('users.lastLogin', { date: formatDate(user.lastLoginAt) })}` : ''}
          </p>
        </div>
        <button type="button" onClick={onClose} aria-label={t('users.close')} className="p-1.5 rounded-lg hover:bg-surface-dark"><X size={16} /></button>
      </div>

      <div className="flex flex-wrap gap-1.5 text-[11px] font-semibold">
        <span className={`px-2 py-0.5 rounded-full ${user.ageGroup === 'adult' ? 'bg-surface-dark text-text-secondary' : 'bg-amber-100 text-amber-900'}`}>{t(`users.age.${user.ageGroup}`)}</span>
        <span className={`px-2 py-0.5 rounded-full ${user.isActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-700'}`}>{user.isActive ? t('users.active') : t('users.inactive')}</span>
        {suspended && <span className="px-2 py-0.5 rounded-full bg-red-100 text-red-700">{t('users.chatSuspendedUntil', { date: formatDateTime(user.chatSuspendedUntil!) })}</span>}
      </div>

      {message && <p role={message.ok ? 'status' : 'alert'} className={`text-sm p-3 rounded-xl ${message.ok ? 'bg-green-50 text-green-800' : 'bg-red-50 text-red-700'}`}>{message.text}</p>}

      <div className="space-y-2">
        <label className="block">
          <span className="text-xs font-semibold text-text-secondary">{t('users.profileType')}</span>
          <select className={`${input} mt-1`} value={profile} onChange={(e) => setProfile(e.target.value as ProfileTypeName)} data-testid="profile-select">
            {PROFILES.map((p) => <option key={p} value={p}>{t(`users.profiles.${p}`)}</option>)}
          </select>
        </label>
        <p className="text-[11px] text-text-muted">{t(`users.profileHints.${profile}`)}</p>
        <input className={input} value={note} maxLength={500} placeholder={t('users.note')} onChange={(e) => setNote(e.target.value)} />
        <Button size="sm" isLoading={busy} disabled={profile === user.profileType}
          onClick={() => confirm(t('users.confirmProfile', { username: user.username, profile: t(`users.profiles.${profile}`) }))
            && run(() => adminUsers.setProfileType(user.id, profile, note || undefined))}>
          {t('users.saveProfile')}
        </Button>
      </div>

      <div className="grid grid-cols-3 gap-2 text-center">
        {([['cards', user.activity.cards], ['trades', user.activity.tradesCompleted], ['flags', user.safety.flags30Days]] as const).map(([k, v]) => (
          <div key={k} className="p-2 rounded-xl bg-surface-dark">
            <dt className="text-[10px] text-text-muted">{t(`users.stats.${k}`)}</dt>
            <dd className={`text-sm font-bold ${k === 'flags' && v > 0 ? 'text-red-700' : ''}`}>{v}</dd>
          </div>
        ))}
      </div>

      {(user.safety.openFlags > 0 || suspended) && (
        <div className="p-3 rounded-xl bg-red-50 space-y-2">
          <p className="text-xs text-red-800 flex items-center gap-1.5"><ShieldAlert size={14} /> {t('users.openFlags', { count: user.safety.openFlags })}</p>
          <div className="flex flex-wrap gap-2">
            <Link to="/moderation" className="text-xs font-semibold text-primary">{t('users.toSafety')}</Link>
            {suspended && <button type="button" disabled={busy} onClick={() => run(() => adminUsers.liftChat(user.id, note || undefined))} className="text-xs font-semibold text-primary">{t('users.liftChat')}</button>}
          </div>
        </div>
      )}

      {user.stores.length > 0 && (
        <ul className="space-y-1">
          {user.stores.map((s) => (
            <li key={s.id}><Link to={`/stores/${s.id}`} className="text-sm text-primary inline-flex items-center gap-1.5"><Store size={13} /> {s.name} · {s.city} · {t(`users.storeStatus.${s.status}`, s.status)}</Link></li>
          ))}
        </ul>
      )}

      {canBan && (
        <button type="button" disabled={busy}
          onClick={() => confirm(user.isActive ? t('users.confirmDeactivate', { username: user.username }) : t('users.confirmReactivate', { username: user.username }))
            && run(() => adminUsers.setActive(user.id, !user.isActive, note || undefined))}
          className={`w-full py-2 rounded-xl text-sm font-semibold ${user.isActive ? 'bg-red-600 text-white' : 'bg-green-600 text-white'}`}>
          {user.isActive ? t('users.deactivate') : t('users.reactivate')}
        </button>
      )}

      <div>
        <h3 className="text-xs font-bold text-text-secondary mb-1">{t('users.audit')}</h3>
        {user.audit.length === 0 ? <p className="text-xs text-text-muted">{t('users.noAudit')}</p> : (
          <ul className="space-y-1.5" data-testid="admin-audit">
            {user.audit.map((a) => (
              <li key={a.id} className="text-xs border-l-2 border-border pl-2">
                <strong>{t(`users.actions.${a.action}`, a.action)}</strong>{a.detail ? ` · ${a.detail.replace(/\b(Collector|Seller|Store|Organizer)\b/g, (m) => t(`users.profiles.${m}`))}` : ''}
                <span className="block text-text-muted">{formatDateTime(a.createdAt)} · {a.admin ? `@${a.admin}` : t('users.automatic')}{a.note ? ` — ${a.note}` : ''}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

/** Utenti per gli amministratori: ricerca, filtri, scheda con tipo di profilo e azioni. */
export default function AdminUsersPage() {
  const { t } = useTranslation('admin');
  const [q, setQ] = useState('');
  const [search, setSearch] = useState('');
  const [profile, setProfile] = useState<ProfileTypeName | ''>('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<{ total: number; pageSize: number; items: AdminUserRow[] } | null>(null);
  const [selected, setSelected] = useState<number | null>(null);

  const load = useCallback(() => {
    adminUsers.list({ q: search || undefined, profileType: profile || undefined, status: status || undefined, page })
      .then(({ data }) => setData(data)).catch(() => setData({ total: 0, pageSize: 20, items: [] }));
  }, [search, profile, status, page]);
  useEffect(() => { load(); }, [load]);

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <div className="pb-24 space-y-4 max-w-3xl">
      <h1 className="text-xl font-bold text-text flex items-center gap-2"><Users size={20} className="text-primary" /> {t('users.title')}</h1>
      <p className="text-sm text-text-secondary">{t('users.intro')}</p>

      <form onSubmit={(e) => { e.preventDefault(); setPage(1); setSearch(q.trim()); }} className="flex gap-2">
        <input className={input} value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('users.search')} aria-label={t('users.search')} />
        <Button type="submit" size="sm" variant="outline"><Search size={15} /></Button>
      </form>
      <div className="grid grid-cols-2 gap-2">
        <select className={input} value={profile} onChange={(e) => { setPage(1); setProfile(e.target.value as ProfileTypeName | ''); }} aria-label={t('users.profileType')}>
          <option value="">{t('users.allProfiles')}</option>
          {PROFILES.map((p) => <option key={p} value={p}>{t(`users.profiles.${p}`)}</option>)}
        </select>
        <select className={input} value={status} onChange={(e) => { setPage(1); setStatus(e.target.value); }} aria-label={t('users.status')}>
          <option value="">{t('users.allStatuses')}</option>
          <option value="active">{t('users.active')}</option>
          <option value="inactive">{t('users.inactive')}</option>
          <option value="chatSuspended">{t('users.chatSuspended')}</option>
        </select>
      </div>

      {selected != null && <UserDetail id={selected} onChanged={load} onClose={() => setSelected(null)} />}

      {data === null ? (
        <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>
      ) : data.items.length === 0 ? (
        <EmptyState icon={Users} title={t('users.empty')} />
      ) : (
        <>
          <p className="text-xs text-text-muted">{t('users.count', { count: data.total })}</p>
          <ul className="divide-y divide-border/60 bg-surface-card rounded-2xl border border-border/60" data-testid="admin-users">
            {data.items.map((u) => (
              <li key={u.id}>
                <button type="button" onClick={() => setSelected(u.id)} className={`w-full text-left p-3 flex items-center gap-3 hover:bg-surface-dark ${selected === u.id ? 'bg-primary/5' : ''}`}>
                  <span className="flex-1 min-w-0">
                    <span className="block text-sm font-semibold text-text truncate">@{u.username}{u.storeName ? <span className="font-normal text-text-secondary"> · {u.storeName}</span> : null}</span>
                    <span className="block text-[11px] text-text-muted truncate">{u.email}{u.city ? ` · ${u.city}` : ''} · {formatDate(u.createdAt)}</span>
                  </span>
                  <span className="shrink-0 flex flex-col items-end gap-0.5 text-[10px] font-semibold">
                    <span className="px-2 py-0.5 rounded-full bg-primary/10 text-primary">{t(`users.profiles.${u.profileType}`)}</span>
                    {!u.isActive && <span className="text-red-700">{t('users.inactive')}</span>}
                    {u.chatSuspended && <span className="text-red-700">{t('users.chatSuspended')}</span>}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          {pages > 1 && (
            <div className="flex items-center justify-center gap-3 text-sm">
              <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(page - 1)}>{t('users.prev')}</Button>
              <span>{page} / {pages}</span>
              <Button size="sm" variant="outline" disabled={page >= pages} onClick={() => setPage(page + 1)}>{t('users.next')}</Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
