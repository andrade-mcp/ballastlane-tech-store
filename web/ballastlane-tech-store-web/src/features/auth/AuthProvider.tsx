import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { auth, tokenStore } from "@/lib/api";
import type { AuthResponse, UserDto } from "@/lib/types";

interface AuthState {
  user: UserDto | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  logout: () => void;
}

const Ctx = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!tokenStore.get()) { setLoading(false); return; }
    auth.get<UserDto>("/api/auth/me")
      .then((r) => setUser(r.data))
      .catch(() => tokenStore.clear())
      .finally(() => setLoading(false));
  }, []);

  const login = async (email: string, password: string) => {
    const { data } = await auth.post<AuthResponse>("/api/auth/login", { email, password });
    tokenStore.set(data.token);
    setUser(data.user);
  };

  const register = async (email: string, password: string, displayName: string) => {
    await auth.post("/api/auth/register", { email, password, displayName });
    await login(email, password);
  };

  const logout = () => { tokenStore.clear(); setUser(null); };

  return <Ctx.Provider value={{ user, loading, login, register, logout }}>{children}</Ctx.Provider>;
}

export function useAuth() {
  const v = useContext(Ctx);
  if (!v) throw new Error("useAuth must be used inside <AuthProvider>");
  return v;
}
