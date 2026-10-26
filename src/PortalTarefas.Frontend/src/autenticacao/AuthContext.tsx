import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";
import { ErroApi, requisitar } from "../api/cliente";

export interface Usuario {
  id: string;
  email: string;
  nome: string;
  papeis: string[];
  equipe: string | null;
  podeEditarOutrasEquipes: boolean;
}

type EstadoSessao = "verificando" | "autenticado" | "anonimo";

interface ValorAuth {
  estado: EstadoSessao;
  usuario: Usuario | null;
  entrar: (email: string, senha: string) => Promise<void>;
  sair: () => Promise<void>;
  temPapel: (...papeis: string[]) => boolean;
}

const AuthContext = createContext<ValorAuth | null>(null);

/**
 * Estado global da sessão. O cookie é HttpOnly: o React nunca vê o token,
 * apenas pergunta à API quem é o usuário (/api/auth/me).
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [estado, setEstado] = useState<EstadoSessao>("verificando");
  const [usuario, setUsuario] = useState<Usuario | null>(null);

  // Restauração de sessão ao abrir/recarregar a SPA
  useEffect(() => {
    requisitar<Usuario>("/api/auth/me")
      .then((u) => { setUsuario(u); setEstado("autenticado"); })
      .catch(() => { setUsuario(null); setEstado("anonimo"); });
  }, []);

  const entrar = useCallback(async (email: string, senha: string) => {
    const u = await requisitar<Usuario>("/api/auth/login", { method: "POST", body: JSON.stringify({ email, senha }) });
    setUsuario(u);
    setEstado("autenticado");
  }, []);

  const sair = useCallback(async () => {
    try { await requisitar<void>("/api/auth/logout", { method: "POST" }); }
    catch (e) { if (!(e instanceof ErroApi && e.status === 401)) throw e; }
    setUsuario(null);
    setEstado("anonimo");
  }, []);

  const temPapel = useCallback((...papeis: string[]) => !!usuario && papeis.some((p) => usuario.papeis.includes(p)), [usuario]);

  return <AuthContext.Provider value={{ estado, usuario, entrar, sair, temPapel }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth deve ser usado dentro de AuthProvider");
  return ctx;
}
