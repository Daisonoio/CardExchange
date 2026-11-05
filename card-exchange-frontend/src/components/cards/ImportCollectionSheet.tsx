import { useEffect, useMemo, useState } from 'react';
import { Trans, useTranslation } from 'react-i18next';
import { AlertTriangle, CheckCircle2, FileUp, Loader2 } from 'lucide-react';
import { catalog, collectionImport } from '../../api';
import { useGame } from '../../context/GameContext';
import type { ImportCommitResult, ImportPreview, ImportPreviewRow } from '../../types';
import { CONDITION_LABELS } from '../../types';
import { apiErrorMessage } from '../../utils/apiError';
import BottomSheet from '../ui/BottomSheet';
import Button from '../ui/Button';
import CardVariantBadges from './CardVariantBadges';

const MAX_FILE_BYTES = 2_000_000;
const ROWS_PAGE = 100;

// Campi riconosciuti: l'etichetta è in collection:import.fields.<campo>
const FIELD_KEYS = [
  'Name', 'SetCode', 'SetName', 'Edition', 'CollectorNumber',
  'ScryfallId', 'Quantity', 'Condition', 'Language', 'Finish', 'Game',
];

type Step = 'pick' | 'preview' | 'done';

/** Import della collezione da CSV (tutti i giochi): scelta gioco e file → anteprima → conferma. */
export default function ImportCollectionSheet({ open, onClose, onImported }: {
  open: boolean;
  onClose: () => void;
  onImported: () => void;
}) {
  const { t } = useTranslation('collection');
  const [step, setStep] = useState<Step>('pick');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [excluded, setExcluded] = useState<Set<number>>(new Set());
  const [makeTradeable, setMakeTradeable] = useState(false);
  const [visibleRows, setVisibleRows] = useState(ROWS_PAGE);
  const [result, setResult] = useState<ImportCommitResult | null>(null);
  const { games, selectedGame } = useGame();
  const importable = games.filter((g) => g.catalogKey);
  const [catalogKey, setCatalogKey] = useState<string>('magic');
  const [hints, setHints] = useState<Record<string, string>>({});

  // Gioco proposto: quello selezionato in alto, altrimenti Magic
  useEffect(() => {
    if (open) setCatalogKey(selectedGame?.catalogKey ?? 'magic');
  }, [open, selectedGame?.catalogKey]);

  useEffect(() => {
    if (!open) return;
    catalog.providers()
      .then(({ data }) => setHints(Object.fromEntries(data.map((p) => [p.key, p.importHint]))))
      .catch(() => setHints({}));
  }, [open]);
  const isMagic = catalogKey === 'magic';
  const gameName = importable.find((g) => g.catalogKey === catalogKey)?.name ?? 'Magic: The Gathering';

  const reset = () => {
    setStep('pick'); setBusy(false); setError(null); setPreview(null);
    setExcluded(new Set()); setMakeTradeable(false); setVisibleRows(ROWS_PAGE); setResult(null);
  };
  const close = () => { reset(); onClose(); };

  const handleFile = async (file: File | undefined) => {
    if (!file) return;
    setError(null);
    if (file.size > MAX_FILE_BYTES) {
      setError(t('import.errors.fileTooLarge'));
      return;
    }
    setBusy(true);
    try {
      const { data } = await collectionImport.preview(await file.text(), isMagic ? undefined : catalogKey);
      setPreview(data);
      setStep('preview');
    } catch (err) {
      setError(apiErrorMessage(err, t('import.errors.readFile')));
    } finally {
      setBusy(false);
    }
  };

  // Errori in cima, così si vedono subito
  const sortedRows = useMemo(
    () => (preview ? [...preview.rows].sort((a, b) => (a.status === b.status ? a.lineNumber - b.lineNumber : a.status === 'error' ? -1 : 1)) : []),
    [preview],
  );
  const selectedRows = useMemo(
    () => (preview?.rows ?? []).filter((r) => r.status === 'ready' && r.match && !excluded.has(r.lineNumber)),
    [preview, excluded],
  );
  const selectedQuantity = selectedRows.reduce((s, r) => s + r.quantity, 0);

  const toggleRow = (row: ImportPreviewRow) =>
    setExcluded((prev) => {
      const next = new Set(prev);
      if (next.has(row.lineNumber)) next.delete(row.lineNumber); else next.add(row.lineNumber);
      return next;
    });

  const handleCommit = async () => {
    setBusy(true);
    setError(null);
    try {
      const rows = selectedRows.map((r) => ({
        scryfallId: r.match!.scryfallId || undefined,
        catalogKey: r.match!.catalogKey,
        externalId: r.match!.externalId,
        quantity: r.quantity,
        condition: r.condition,
        language: r.language,
        finish: r.finish,
      }));
      const { data } = await collectionImport.commit(rows, makeTradeable);
      setResult(data);
      setStep('done');
      onImported();
    } catch (err) {
      setError(apiErrorMessage(err, t('import.errors.import')));
    } finally {
      setBusy(false);
    }
  };

  return (
    <BottomSheet open={open} onClose={close} title={t('import.title')}>
      {error && <div className="mb-3 px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}

      {step === 'pick' && (
        <div className="space-y-4">
          {importable.length > 1 && (
            <label className="block">
              <span className="block text-xs font-medium text-text-secondary mb-1">{t('import.fileGame')}</span>
              <select
                value={catalogKey}
                onChange={(e) => setCatalogKey(e.target.value)}
                className="w-full px-3 py-2.5 rounded-xl border border-border bg-white text-sm text-text"
              >
                {importable.map((g) => <option key={g.id} value={g.catalogKey}>{g.name}</option>)}
              </select>
            </label>
          )}
          <p className="text-sm text-text-secondary">
            {isMagic
              ? <Trans t={t} i18nKey="import.introMagic" components={{ strong: <strong /> }} />
              : <Trans t={t} i18nKey="import.introOther" values={{ game: gameName }} components={{ strong: <strong /> }} />}
            {' '}{t('import.previewNote')}
          </p>
          <ul className="text-xs text-text-muted list-disc pl-5 space-y-0.5">
            {isMagic ? (
              <>
                <li>{t('import.tipNameColumn')}</li>
                <li>{t('import.tipExactMatch')}</li>
              </>
            ) : (
              <li>{t('import.tipRecognize', { hint: hints[catalogKey] ?? t('import.defaultHint') })}</li>
            )}
            <li>{t('import.tipFields')}</li>
            <li>{t('import.tipMultiGame')}</li>
            {!isMagic && <li>{t('import.tipSlow')}</li>}
          </ul>
          <label className={`flex flex-col items-center justify-center gap-2 p-6 rounded-2xl border-2 border-dashed border-border cursor-pointer hover:bg-surface-dark transition-colors ${busy ? 'opacity-60 pointer-events-none' : ''}`}>
            {busy ? <Loader2 size={28} className="animate-spin text-primary" /> : <FileUp size={28} className="text-primary" />}
            <span className="text-sm font-semibold text-text">{busy ? t('import.analyzing') : t('import.chooseFile')}</span>
            <span className="text-[11px] text-text-muted">{t('import.limits', { rows: isMagic ? '5000' : '1000' })}</span>
            <input
              type="file"
              accept=".csv,.txt,text/csv"
              className="sr-only"
              onChange={(e) => { handleFile(e.target.files?.[0]); e.target.value = ''; }}
            />
          </label>
        </div>
      )}

      {step === 'preview' && preview && (
        <div className="space-y-4">
          <div className="grid grid-cols-3 gap-2 text-center">
            <div className="rounded-xl bg-surface-dark p-2">
              <p className="text-lg font-bold text-text">{preview.readyCount}</p>
              <p className="text-[10px] text-text-muted">{t('import.readyRows')}</p>
            </div>
            <div className={`rounded-xl p-2 ${preview.errorCount ? 'bg-red-50' : 'bg-surface-dark'}`}>
              <p className={`text-lg font-bold ${preview.errorCount ? 'text-danger' : 'text-text'}`}>{preview.errorCount}</p>
              <p className="text-[10px] text-text-muted">{t('import.withErrors')}</p>
            </div>
            <div className="rounded-xl bg-surface-dark p-2">
              <p className="text-lg font-bold text-text">{preview.totalQuantity}</p>
              <p className="text-[10px] text-text-muted">{t('import.totalCopies')}</p>
            </div>
          </div>

          <div className="text-xs text-text-secondary space-y-1">
            <p><Trans t={t} i18nKey="import.detectedFormat" values={{ format: preview.detectedFormat }} components={{ strong: <strong /> }} /></p>
            <div className="flex flex-wrap gap-1">
              {Object.entries(preview.mapping).map(([field, header]) => (
                <span key={field} className="px-2 py-0.5 rounded-full bg-primary/10 text-primary text-[10px]">
                  {FIELD_KEYS.includes(field) ? t(`import.fields.${field}`) : field} ← {header}
                </span>
              ))}
            </div>
            {preview.ignoredColumns.length > 0 && (
              <p className="text-[10px] text-text-muted">{t('import.ignoredColumns', { columns: preview.ignoredColumns.join(', ') })}</p>
            )}
          </div>

          <div className="flex items-center justify-between p-3 bg-surface-dark rounded-xl">
            <div>
              <p className="text-sm font-medium">{t('import.allToTrade')}</p>
              <p className="text-xs text-text-muted">{t('import.allToTradeHint')}</p>
            </div>
            <button
              type="button"
              aria-pressed={makeTradeable}
              onClick={() => setMakeTradeable(!makeTradeable)}
              className={`w-12 h-7 rounded-full transition-colors relative ${makeTradeable ? 'bg-primary' : 'bg-gray-300'}`}
            >
              <div className={`w-5 h-5 bg-white rounded-full absolute top-1 transition-transform ${makeTradeable ? 'translate-x-6' : 'translate-x-1'}`} />
            </button>
          </div>

          <div className="space-y-1.5 max-h-[45vh] overflow-y-auto pr-1">
            {sortedRows.slice(0, visibleRows).map((row) => (
              <PreviewRow key={row.lineNumber} row={row} excluded={excluded.has(row.lineNumber)} onToggle={() => toggleRow(row)} />
            ))}
            {sortedRows.length > visibleRows && (
              <button
                type="button"
                onClick={() => setVisibleRows((v) => v + ROWS_PAGE)}
                className="w-full py-2 text-xs font-semibold text-primary hover:underline"
              >
                {t('import.showMoreRows', { count: Math.min(ROWS_PAGE, sortedRows.length - visibleRows) })}
              </button>
            )}
          </div>

          <div className="flex gap-2">
            <Button variant="outline" className="flex-1" onClick={reset} disabled={busy}>{t('import.changeFile')}</Button>
            <Button className="flex-1" onClick={handleCommit} isLoading={busy} disabled={selectedRows.length === 0}>
              {t('import.submit', { count: selectedQuantity })}
            </Button>
          </div>
        </div>
      )}

      {step === 'done' && result && (
        <div className="space-y-4">
          <div className="flex items-center gap-3 p-4 rounded-xl bg-green-50 text-green-800">
            <CheckCircle2 size={24} className="shrink-0" />
            <div className="text-sm">
              <p className="font-semibold">{t('import.imported', { count: result.totalQuantity })}</p>
              <p className="text-xs">
                {t('import.createdEntries', { count: result.createdCopies })}
                {result.mergedIntoExisting > 0 && t('import.merged', { count: result.mergedIntoExisting })}
              </p>
            </div>
          </div>
          {result.skippedByPlanLimit > 0 && (
            <div className="flex gap-2 p-3 rounded-xl bg-amber-50 text-amber-800 text-xs">
              <AlertTriangle size={16} className="shrink-0 mt-0.5" />
              <p>
                {t('import.planLimit', { count: result.skippedByPlanLimit, limit: result.planCardLimit })}
              </p>
            </div>
          )}
          {result.failed.length > 0 && (
            <div className="p-3 rounded-xl bg-red-50 text-danger text-xs">
              {t('import.failed', { count: result.failed.length, list: result.failed.slice(0, 5).join('; ') })}
              {result.failed.length > 5 && '…'}
            </div>
          )}
          <Button className="w-full" onClick={close}>{t('import.done')}</Button>
        </div>
      )}
    </BottomSheet>
  );
}

function PreviewRow({ row, excluded, onToggle }: { row: ImportPreviewRow; excluded: boolean; onToggle: () => void }) {
  const { t } = useTranslation('collection');
  const isError = row.status === 'error';
  return (
    <div className={`flex items-center gap-2 p-2 rounded-xl border ${isError ? 'border-red-200 bg-red-50/50' : excluded ? 'border-border opacity-50' : 'border-border bg-white'}`}>
      {!isError && (
        <input type="checkbox" checked={!excluded} onChange={onToggle} aria-label={t('import.includeRow', { line: row.lineNumber })} className="accent-primary" />
      )}
      {row.match?.imageSmall ? (
        <img src={row.match.imageSmall} alt="" className="w-8 h-11 rounded object-cover shrink-0" loading="lazy" />
      ) : (
        <div className="w-8 h-11 rounded bg-gray-100 shrink-0" />
      )}
      <div className="flex-1 min-w-0">
        <p className="text-xs font-semibold truncate">
          {row.match?.name ?? row.inputName ?? '—'}
          <span className="font-normal text-text-muted"> · {t('import.line', { line: row.lineNumber })}</span>
        </p>
        {isError ? (
          <p className="text-[10px] text-danger truncate">
            {row.error}{row.inputSet ? ` (${row.inputSet}${row.inputNumber ? ` #${row.inputNumber}` : ''})` : ''}
          </p>
        ) : (
          <div className="flex items-center gap-1 flex-wrap text-[10px] text-text-muted">
            <span className="uppercase">{row.match?.setCode}{row.match?.collectorNumber ? ` #${row.match.collectorNumber}` : ''}</span>
            <span>· {CONDITION_LABELS[row.condition]}</span>
            <CardVariantBadges language={row.language} finish={row.finish} />
          </div>
        )}
        {row.warnings.map((w) => (
          <p key={w} className="text-[10px] text-amber-700 truncate" title={w}>{w}</p>
        ))}
      </div>
      <span className="text-xs font-semibold text-text-secondary shrink-0">×{row.quantity}</span>
    </div>
  );
}
