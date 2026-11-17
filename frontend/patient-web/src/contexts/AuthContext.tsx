import React, { createContext, useContext, useEffect, useState } from 'react';
import { UserProfile, LoginResponse } from '../types/auth';
import { apiClient } from '../api/client';

interface AuthContextType {
  user: UserProfile | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (data: LoginResponse) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserProfile | null>(() => {
    const saved = localStorage.getItem('patient_user');
    return saved ? JSON.parse(saved) : null;
  });
  const [token, setToken] = useState<string | null>(() => localStorage.getItem('patient_token'));
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const initAuth = async () => {
      const storedToken = localStorage.getItem('patient_token');
      if (storedToken) {
        try {
          const res = await apiClient.get<UserProfile>('/me');
          if (res.data.role !== 'Patient') {
            // Chặn tài khoản nhân viên y tế vào kênh bệnh nhân (Channel & Role Policy)
            logout();
          } else {
            setUser(res.data);
            localStorage.setItem('patient_user', JSON.stringify(res.data));
          }
        } catch {
          logout();
        }
      }
      setIsLoading(false);
    };

    initAuth();
  }, []);

  const login = (data: LoginResponse) => {
    if (data.role !== 'Patient') {
      throw new Error('Tài khoản nhân viên y tế không được phép đăng nhập kênh bệnh nhân.');
    }
    setToken(data.accessToken);
    const profile: UserProfile = {
      userId: data.userId,
      userName: data.userName,
      role: data.role,
    };
    setUser(profile);
    localStorage.setItem('patient_token', data.accessToken);
    localStorage.setItem('patient_user', JSON.stringify(profile));
  };

  const logout = () => {
    setToken(null);
    setUser(null);
    localStorage.removeItem('patient_token');
    localStorage.removeItem('patient_user');
  };

  return (
    <AuthContext.Provider value={{ user, token, isAuthenticated: !!token && !!user, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
