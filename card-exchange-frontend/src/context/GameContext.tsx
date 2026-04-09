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
  setSelectedGameId: (id: number) => void;
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
    return stored ? parseInt(stored, 10) : null;
  });
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    client.get<GameInfo[]>('/games')
      .then(({ data }) => {
        const list = Array.isArray(data) ? data : (data as any)?.games ?? [];
        const active = list.filter((g: GameInfo) => g.isActive);
        setGames(active);

        // If no game selected or selected game not found, default to first
        if (active.length > 0) {
          const storedId = localStorage.getItem(STORAGE_KEY);
          const storedNum = storedId ? parseInt(storedId, 10) : null;
          if (!storedNum || !active.find((g: GameInfo) => g.id === storedNum)) {
            setSelectedGameIdState(active[0].id);
            localStorage.setItem(STORAGE_KEY, String(active[0].id));
          }
        }
      })
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, []);

  const setSelectedGameId = useCallback((id: number) => {
    setSelectedGameIdState(id);
    localStorage.setItem(STORAGE_KEY, String(id));
  }, []);

  const selectedGame = games.find(g => g.id === selectedGameId) ?? null;

  return (
    <GameContext.Provider value={{ games, selectedGame, selectedGameId, setSelectedGameId, isLoading }}>
      {children}
    </GameContext.Provider>
  );
}
