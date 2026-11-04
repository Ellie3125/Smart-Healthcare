import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';
import type { ReactNode } from 'react';
import { api } from '../services/api';
import type { AuthResponse, CurrentUser, ProblemDetails } from '../types/auth';

interface AuthContextValue {
  user: CurrentUser | null;
  token: string | null;
  isLoading: boolean;
  loginAsStaff: (userName: string, password: string) => Promise<void>;
  logout: () => void;
  isAuthenticated: boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

const TOKEN_KEY = 'access_token';
const USER_KEY = 'current_user';

export function AuthProvider({ children }: { readonly children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() =>
    localStorage.getItem(TOKEN_KEY)
  );
  const [user, setUser] = useState<CurrentUser | null>(() => {
    try {
      const stored = localStorage.getItem(USER_KEY);
      return stored ? (JSON.parse(stored) as CurrentUser) : null;
    } catch {
      return null;
    }
  });
  const [isLoading, setIsLoading] = useState(false);

  // Nếu token hết hạn thì xóa session khi khởi động
  useEffect(() => {
    if (token && !user) {
      localStorage.removeItem(TOKEN_KEY);
      setToken(null);
    }
  }, [token, user]);

  const loginAsStaff = useCallback(
    async (userName: string, password: string) => {
      setIsLoading(true);
      try {
        const { data } = await api.post<AuthResponse>('/auth/staff/login', {
          userName,
          password,
        });
        localStorage.setItem(TOKEN_KEY, data.accessToken);
        localStorage.setItem(USER_KEY, JSON.stringify(data.user));
        setToken(data.accessToken);
        setUser(data.user);
      } catch (err: unknown) {
        // Ném lại để UI hiển thị lỗi
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    []
  );

  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    setToken(null);
    setUser(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      token,
      isLoading,
      loginAsStaff,
      logout,
      isAuthenticated: !!token && !!user,
    }),
    [user, token, isLoading, loginAsStaff, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuth phải được dùng bên trong AuthProvider');
  }
  return ctx;
}

