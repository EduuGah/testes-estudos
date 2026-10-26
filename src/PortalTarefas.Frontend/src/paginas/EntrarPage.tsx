import { useState, type FormEvent } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../autenticacao/AuthContext";

export function EntrarPage() {
  const { entrar } = useAuth();
  const navegar = useNavigate();
  const destino = (useLocation().state as { destino?: string } | null)?.destino ?? "/";
  const [erro, setErro] = useState<string>();
  const [enviando, setEnviando] = useState(false);

  async function enviar(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    setEnviando(true); setErro(undefined);
    try {
      await entrar(String(f.get("email")), String(f.get("senha")));
      navegar(destino, { replace: true });   // volta para onde o usuário queria ir
    } catch {
      setErro("E-mail ou senha inválidos.");   // mesma mensagem para qualquer falha
    } finally {
      setEnviando(false);
    }
  }

  return (
    <section>
      <h1>Entrar</h1>
      {erro && <div className="estado erro" role="alert">{erro}</div>}
      <form className="formulario" onSubmit={enviar}>
        <div className="campo"><label htmlFor="email">E-mail</label><input id="email" name="email" type="email" autoComplete="username" required /></div>
        <div className="campo"><label htmlFor="senha">Senha</label><input id="senha" name="senha" type="password" autoComplete="current-password" required /></div>
        <button type="submit" disabled={enviando}>{enviando ? "Entrando..." : "Entrar"}</button>
      </form>
    </section>
  );
}
