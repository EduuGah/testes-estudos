import { Navigate, Outlet, useLocation } from "react-router-dom";
import { Carregando } from "../componentes/Estados";
import { useAuth } from "./AuthContext";

/**
 * Protege rotas da SPA. É só experiência do usuário: a API continua
 * recusando (401/403) qualquer chamada sem permissão.
 */
export function RotaProtegida({ papeis }: { papeis?: string[] }) {
  const { estado, temPapel } = useAuth();
  const local = useLocation();

  if (estado === "verificando") return <Carregando texto="Verificando sessão..." />;
  if (estado === "anonimo") {
    // Guarda a URL pretendida para voltar a ela depois do login
    return <Navigate to="/entrar" replace state={{ destino: local.pathname + local.search }} />;
  }
  if (papeis && !temPapel(...papeis)) return <div className="estado erro" role="alert">Você não tem permissão para acessar esta página.</div>;
  return <Outlet />;
}
