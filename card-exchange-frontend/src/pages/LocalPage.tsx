import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import StoresTab from '../components/stores/StoresTab';
import EventsPage from './EventsPage';

type Tab = 'events' | 'stores';

/** "Vicino a te": eventi e negozi della zona. */
export default function LocalPage() {
  const { t } = useTranslation('events');
  const [params, setParams] = useSearchParams();
  const tab: Tab = params.get('tab') === 'stores' ? 'stores' : 'events';

  return (
    <div className="pb-20">
      <h1 className="text-xl font-bold text-text mb-3">{t('local.title')}</h1>
      <div className="flex rounded-xl border border-border overflow-hidden bg-white mb-4" role="tablist">
        {([['events', 'local.tabs.events'], ['stores', 'local.tabs.stores']] as [Tab, string][]).map(([key, labelKey]) => (
          <button
            key={key}
            role="tab"
            aria-selected={tab === key}
            onClick={() => setParams({ tab: key }, { replace: true })}
            className={`flex-1 py-2 text-sm font-medium ${tab === key ? 'bg-primary text-white' : 'text-text-secondary'}`}
          >
            {t(labelKey)}
          </button>
        ))}
      </div>
      {tab === 'stores' ? <StoresTab /> : <EventsPage />}
    </div>
  );
}
