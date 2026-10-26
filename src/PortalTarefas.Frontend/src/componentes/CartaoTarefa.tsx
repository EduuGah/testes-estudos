import { Link } from "react-router-dom";
import type { TarefaResumo } from "../api/tipos";

const formatarData = (d: string | null) => (d ? new Date(d + "T00:00:00").toLocaleDateString("pt-BR") : "sem prazo");

export function CartaoTarefa({ tarefa }: { tarefa: TarefaResumo }) {
  return (
    <article className={`cartao ${tarefa.vencida ? "vencida" : ""}`}>
      <div className="cartao-topo">
        <Link to={`/tarefas/${tarefa.id}`} className="titulo">#{tarefa.id} · {tarefa.titulo}</Link>
        <span className={`selo prioridade-${tarefa.prioridade.toLowerCase()}`}>{tarefa.prioridade}</span>
      </div>
      <div className="meta">
        <span className="selo">{tarefa.situacao}</span>
        <span>Prazo: {formatarData(tarefa.prazo)}</span>
        {tarefa.vencida && <span className="alerta">vencida</span>}
      </div>
    </article>
  );
}
