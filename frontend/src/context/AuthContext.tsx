import { createContext, useContext, useState, ReactNode } from "react";
import { login as apiLogin, clearTokens } from "../api/client";

interface AuthCtx { isAuthed: boolean; login: (u: string, p: string) => Promise<void>; logout: () => void; }
const Ctx = createContext<AuthCtx>(null as any);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthed, setAuthed] = useState(!!localStorage.getItem("access_token"));
  return (
    <Ctx.Provider value={{
      isAuthed,
      login: async (u, p) => { await apiLogin(u, p); setAuthed(true); },
      logout: () => { clearTokens(); setAuthed(false); },
    }}>
      {children}
    </Ctx.Provider>
  );
}
export const useAuth = () => useContext(Ctx);
