import React, { createContext, useState, useEffect } from 'react';
import { CurrentUserDto, LoginResponse } from '../types/patient';

export interface AuthContextType {
  user: CurrentUserDto | null;
  token: string | null;
  isAuthenticated: boolean;
  login: (data: LoginResponse) => void;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem('patient_token'));
  const [user, setUser] = useState<CurrentUserDto | null>(() => {
    const raw = localStorage.getItem('patient_user');
    return raw ? JSON.parse(raw) : null;
  });

  useEffect(() => {
    if (token) {
      localStorage.setItem('patient_token', token);
    } else {
      localStorage.removeItem('patient_token');
    }
  }, [token]);

  useEffect(() => {
    if (user) {
      localStorage.setItem('patient_user', JSON.stringify(user));
    } else {
      localStorage.removeItem('patient_user');
    }
  }, [user]);

  const login = (data: LoginResponse) => {
    setToken(data.token);
    setUser({
      userId: data.userId,
      userName: data.userName,
      role: data.role,
    });
  };

  const logout = () => {
    setToken(null);
    setUser(null);
    localStorage.removeItem('patient_token');
    localStorage.removeItem('patient_user');
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: !!token && !!user,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};
