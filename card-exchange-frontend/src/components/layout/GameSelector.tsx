import { useTranslation } from 'react-i18next';
import { useGame } from '../../context/GameContext';

/** Short display names and emoji for each game */
const GAME_META: Record<string, { short: string; color: string }> = {
  'Magic: The Gathering': { short: 'Magic', color: 'bg-violet-600' },
  'Pokémon TCG': { short: 'Pokémon', color: 'bg-yellow-500' },
  'Yu-Gi-Oh!': { short: 'Yu-Gi-Oh!', color: 'bg-red-600' },
  'One Piece TCG': { short: 'One Piece', color: 'bg-sky-500' },
};

export default function GameSelector() {
  const { t } = useTranslation('layout');
  const { games, selectedGameId, setSelectedGameId, isLoading } = useGame();

  if (isLoading || games.length === 0) return null;

  return (
    <div className="sticky top-14 md:top-16 z-40 bg-night/90 backdrop-blur border-b border-border/50">
      <div className="max-w-2xl mx-auto px-4">
        <div className="flex gap-2 py-2.5 overflow-x-auto scrollbar-hide">
          <button
            onClick={() => setSelectedGameId(null)}
            className={`px-5 py-1.5 rounded-full text-sm font-semibold whitespace-nowrap transition-all shrink-0 ${
              selectedGameId === null
                ? 'bg-primary text-on-primary shadow-sm'
                : 'bg-surface-card border border-border text-text hover:bg-surface-raised'
            }`}
          >
            {t('gameSelector.all')}
          </button>
          {games.map((game) => {
            const meta = GAME_META[game.name] ?? { short: game.name, color: 'bg-gray-500' };
            const isSelected = game.id === selectedGameId;

            return (
              <button
                key={game.id}
                onClick={() => setSelectedGameId(game.id)}
                className={`px-5 py-1.5 rounded-full text-sm font-semibold whitespace-nowrap transition-all shrink-0 ${
                  isSelected
                    ? 'bg-primary text-on-primary shadow-sm'
                    : 'bg-surface-card border border-border text-text hover:bg-surface-raised'
                }`}
              >
                {meta.short}
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
