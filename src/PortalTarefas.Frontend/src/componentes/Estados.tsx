import type { ErroApi } from "../api/cliente";

/** Anúncios em região aria-live: leitores de tela informam carregamento e erro. */
export function Carregando({ texto = "Carregando tarefas..." }: { texto?: string }) {
  return <div className="estado carregando" role="status" aria-live="polite"><span className="spinner" aria-hidden /> {texto}</div>;
}

export function Vazio({ texto }: { texto: string }) {
  return <div className="estado vazio" role="status" aria-live="polite">{texto}</div>;
}

export function Ocioso() {
  return <div className="estado" role="status">Preparando consulta...</div>;
}

export function MensagemErro({ erro, aoTentarNovamente }: { erro: ErroApi; aoTentarNovamente?: () => void }) {
  const titulo = erro.tipo === "rede" ? "Sem conexão com a API" : `Erro ${erro.status ?? ""}`.trim();
  return (
    <div className="estado erro" role="alert">
      <strong>{titulo}</strong>
      <p>{erro.message}</p>
      {aoTentarNovamente && <button onClick={aoTentarNovamente}>Tentar novamente</button>}
    </div>
  );
}
