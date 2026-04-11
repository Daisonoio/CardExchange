import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import client from '../api/client';

export interface GameInfo {
  id: number;
  name: string;
  description?: string;
  publisher: string;
  isActive: boolean;
}

interface GameContextType {
  games: GameInfo[];
  selectedGame: GameInfo | null;
  selectedGameId: number | null;
  setSelectedGameId: (id: number | null) => void;
  isLoading: boolean;
}

const GameContext = createContext<GameContextType>({
  games: [],
  selectedGame: null,
  selectedGameId: null,
  setSelectedGameId: () => {},
  isLoading: true,
});

export function useGame() {
  return useContext(GameContext);
}

const STORAGE_KEY = 'cardexchange_selected_game';

export function GameProvider({ children }: { children: ReactNode }) {
  const [games, setGames] = useState<GameInfo[]>([]);
  const [selectedGameId, setSelectedGameIdState] = useState<number | null>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored || stored === 'all') return null;
    return parseInt(stored, 10);
  });
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    client.get<GameInfo[]>('/games')
      .then(({ data }) => {
        const list = Array.isArray(data) ? data : (data as any)?.games ?? [];
        const active = list.filter((g: GameInfo) => g.isActive);
        setGames(active);

        // If stored game is not found among active games, reset to null (all games)
        if (active.length > 0) {
          const storedId = localStorage.getItem(STORAGE_KEY);
          if (storedId !== null && storedId !== 'all') {
            const storedNum = parseInt(storedId, 10);
            if (!active.find((g: GameInfo) => g.id === storedNum)) {
              setSelectedGameIdState(null);
              localStorage.setItem(STORAGE_KEY, 'all');
            }
          }
        }
      })
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, []);

  const setSelectedGameId = useCallback((id: number | null) => {
    setSelectedGameIdState(id);
    localStorage.setItem(STORAGE_KEY, id === null ? 'all' : String(id));
  }, []);

  const selectedGame = games.find(g => g.id === selectedGameId) ?? null;

  return (
    <GameContext.Provider value={{ games, selectedGame, selectedGameId, setSelectedGameId, isLoading }}>
      {children}
    </GameContext.Provider>
  );
}
