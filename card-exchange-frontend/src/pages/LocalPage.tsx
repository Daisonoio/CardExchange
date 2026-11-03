import { useSearchParams } from 'react-router-dom';
import StoresTab from '../components/stores/StoresTab';
import EventsPage from './EventsPage';

type Tab = 'events' | 'stores';

/** "Vicino a te": eventi e negozi della zona. */
export default function LocalPage() {
  const [params, setParams] = useSearchParams();
  const tab: Tab = params.get('tab') === 'stores' ? 'stores' : 'events';

  return (
    <div className="pb-20">
      <h1 className="text-xl font-bold text-text mb-3">Vicino a te</h1>
      <div className="flex rounded-xl border border-border overflow-hidden bg-white mb-4" role="tablist">
        {([['events', 'Eventi'], ['stores', 'Negozi']] as [Tab, string][]).map(([key, label]) => (
          <button
            key={key}
            role="tab"
            aria-selected={tab === key}
            onClick={() => setParams({ tab: key }, { replace: true })}
            className={`flex-1 py-2 text-sm font-medium ${tab === key ? 'bg-primary text-white' : 'text-text-secondary'}`}
          >
            {label}
          </button>
        ))}
      </div>
      {tab === 'stores' ? <StoresTab /> : <EventsPage />}
    </div>
  );
}
