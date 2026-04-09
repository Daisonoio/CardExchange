import { useGame } from '../../context/GameContext';

/** Short display names and emoji for each game */
const GAME_META: Record<string, { short: string; color: string }> = {
  'Magic: The Gathering': { short: 'Magic', color: 'bg-violet-600' },
  'Pokémon TCG': { short: 'Pokémon', color: 'bg-yellow-500' },
  'Yu-Gi-Oh!': { short: 'Yu-Gi-Oh!', color: 'bg-red-600' },
  'One Piece TCG': { short: 'One Piece', color: 'bg-sky-500' },
};

export default function GameSelector() {
  const { games, selectedGameId, setSelectedGameId, isLoading } = useGame();

  if (isLoading || games.length <= 1) return null;

  return (
    <div className="sticky top-0 md:top-16 z-40 bg-white/90 backdrop-blur-md border-b border-border/50">
      <div className="max-w-7xl mx-auto px-2 md:px-4">
        <div className="flex gap-1 py-1.5 overflow-x-auto scrollbar-hide">
          {games.map((game) => {
            const meta = GAME_META[game.name] ?? { short: game.name, color: 'bg-gray-500' };
            const isSelected = game.id === selectedGameId;

            return (
              <button
                key={game.id}
                onClick={() => setSelectedGameId(game.id)}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-semibold whitespace-nowrap transition-all shrink-0 ${
                  isSelected
                    ? `${meta.color} text-white shadow-sm`
                    : 'bg-gray-100 text-text-secondary hover:bg-gray-200'
                }`}
              >
                <span className={`w-2 h-2 rounded-full ${isSelected ? 'bg-white/80' : meta.color}`} />
                {meta.short}
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
