import { syncTimeZone } from '../utils/timeZone';
import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import type { User, LoginRequest, RegisterRequest } from '../types';
import { auth } from '../api';

interface AuthContextType {
  user: User | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  login: (data: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  logout: () => void;
  refreshUser: () => Promise<void>;
  /** Il server non risponde all'avvio (rete assente, server sovraccarico): la sessione resta, si può riprovare. */
  connectionError: boolean;
  retryConnection: () => void;
}

type LoadResult = 'ok' | 'unauthorized' | 'unavailable';

const RETRY_DELAYS_MS = [1000, 2500];

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const [connectionError, setConnectionError] = useState(false);

  // Fuso orario del dispositivo: gli orari nelle notifiche arrivano nell'ora giusta anche fuori dall'Italia
  const userId = user?.id;
  useEffect(() => {
    if (userId) syncTimeZone(userId);
  }, [userId]);

  /**
   * Carica l'utente. La sessione si chiude solo se il server dice che non è più valida (401/403):
   * con la rete assente o il server momentaneamente non disponibile si resta collegati.
   */
  const loadUser = useCallback(async (): Promise<LoadResult> => {
    try {
      const { data } = await auth.me();
      setUser(data);
      return 'ok';
    } catch (err) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 401 || status === 403) {
        setUser(null);
        localStorage.removeItem('token');
        localStorage.removeItem('refreshToken');
        return 'unauthorized';
      }
      return 'unavailable';
    }
  }, []);

  const refreshUser = useCallback(async () => {
    await loadUser();
  }, [loadUser]);

  const bootstrap = useCallback(async () => {
    if (!localStorage.getItem('token')) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    setConnectionError(false);
    let result = await loadUser();
    for (const delay of RETRY_DELAYS_MS) {
      if (result !== 'unavailable') break;
      await new Promise((r) => setTimeout(r, delay));
      result = await loadUser();
    }
    setConnectionError(result === 'unavailable');
    setIsLoading(false);
  }, [loadUser]);

  useEffect(() => {
    void bootstrap();
  }, [bootstrap]);

  const login = async (data: LoginRequest) => {
    const res = await auth.login(data);
    localStorage.setItem('token', res.data.accessToken);
    localStorage.setItem('refreshToken', res.data.refreshToken);
    setUser(res.data.user);
  };

  const register = async (data: RegisterRequest) => {
    const res = await auth.register(data);
    localStorage.setItem('token', res.data.accessToken);
    localStorage.setItem('refreshToken', res.data.refreshToken);
    setUser(res.data.user);
  };

  const logout = () => {
    auth.logout().catch(() => {});
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    setUser(null);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        isAuthenticated: !!user,
        login,
        register,
        logout,
        refreshUser,
        connectionError,
        retryConnection: () => { void bootstrap(); },
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within AuthProvider');
  return context;
}
