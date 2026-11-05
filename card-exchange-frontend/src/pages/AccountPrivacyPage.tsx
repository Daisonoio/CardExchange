import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Download, FileText, ShieldCheck, Trash2 } from 'lucide-react';
import { account } from '../api';
import { useAuth } from '../context/AuthContext';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';

/** Account e privacy: scaricare tutti i propri dati, eliminare l'account, documenti legali. */
export default function AccountPrivacyPage() {
  const { t } = useTranslation('legal');
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [typed, setTyped] = useState('');
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  if (!user) return null;

  const download = async () => {
    setExporting(true);
    setExportError(null);
    try {
      const { data } = await account.exportData();
      const url = URL.createObjectURL(data);
      const a = document.createElement('a');
      a.href = url;
      a.download = `cardexchange-dati-${user.username}.json`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      setExportError(t('exportError'));
    } finally {
      setExporting(false);
    }
  };

  const remove = async () => {
    setDeleting(true);
    setDeleteError(null);
    try {
      await account.deleteAccount(typed.trim());
      logout();
      navigate('/login', { replace: true });
    } catch (err) {
      setDeleteError(apiErrorMessage(err, t('deleteError')));
      setDeleting(false);
    }
  };

  const card = 'bg-white rounded-2xl border border-border/60 p-4 space-y-2';
  return (
    <div className="pb-24 max-w-lg mx-auto space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0"><ArrowLeft size={18} /></button>
        <h1 className="text-xl font-bold text-text flex items-center gap-2"><ShieldCheck size={20} className="text-primary" /> {t('account.title')}</h1>
      </div>

      <section className={card}>
        <h2 className="font-semibold text-text flex items-center gap-2"><Download size={16} className="text-primary" /> {t('account.exportTitle')}</h2>
        <p className="text-sm text-text-secondary">{t('account.exportBody')}</p>
        <Button onClick={download} isLoading={exporting} variant="outline" size="sm">{exporting ? t('account.exporting') : t('account.exportButton')}</Button>
        {exportError && <p role="alert" className="text-sm text-danger">{exportError}</p>}
      </section>

      <section className={card}>
        <h2 className="font-semibold text-text flex items-center gap-2"><FileText size={16} className="text-primary" /> {t('account.legalTitle')}</h2>
        <div className="flex flex-wrap gap-3 text-sm font-semibold">
          <Link to="/privacy" className="text-primary hover:underline">{t('nav.privacy')}</Link>
          <Link to="/terms" className="text-primary hover:underline">{t('nav.terms')}</Link>
          <Link to="/cookies" className="text-primary hover:underline">{t('nav.cookies')}</Link>
        </div>
        <p className="text-xs text-text-muted">{t('account.termsAccepted')}</p>
      </section>

      <section className={`${card} border-red-200`}>
        <h2 className="font-semibold text-danger flex items-center gap-2"><Trash2 size={16} /> {t('account.deleteTitle')}</h2>
        <p className="text-sm text-text-secondary">{t('account.deleteBody')}</p>
        {!confirming ? (
          <button type="button" onClick={() => setConfirming(true)} className="text-sm font-semibold text-danger hover:underline">{t('account.deleteStart')}</button>
        ) : (
          <div className="space-y-2">
            <label htmlFor="confirm-username" className="block text-xs font-semibold text-text-secondary">{t('account.deleteConfirmLabel', { username: user.username })}</label>
            <input id="confirm-username" value={typed} onChange={(e) => setTyped(e.target.value)} autoComplete="off" className="w-full px-3 py-2.5 rounded-xl border border-red-200 text-sm focus:outline-none focus:ring-2 focus:ring-red-200" />
            <button
              type="button"
              onClick={remove}
              disabled={typed.trim() !== user.username || deleting}
              className="w-full py-2.5 rounded-xl bg-danger text-white text-sm font-semibold disabled:opacity-40"
            >
              {deleting ? t('account.deleting') : t('account.deleteConfirm')}
            </button>
            {deleteError && <p role="alert" className="text-sm text-danger">{deleteError}</p>}
          </div>
        )}
      </section>
    </div>
  );
}
