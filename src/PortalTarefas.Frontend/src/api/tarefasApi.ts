import { requisitar } from "./cliente";
import type { CriarTarefa, FiltrosTarefas, Pagina, TarefaDetalhe, TarefaResumo } from "./tipos";

/** Único módulo que conhece as URLs do recurso tarefas. */
export const tarefasApi = {
  listar(filtros: FiltrosTarefas, sinal?: AbortSignal) {
    const q = new URLSearchParams({
      pagina: String(filtros.pagina),
      tamanho: String(filtros.tamanho),
      ordenarPor: filtros.ordenarPor,
      direcao: filtros.direcao,
    });
    if (filtros.situacao) q.set("situacao", filtros.situacao);
    if (filtros.busca) q.set("busca", filtros.busca);
    return requisitar<Pagina<TarefaResumo>>(`/api/v1/tarefas?${q}`, { signal: sinal });
  },

  obter(id: number, sinal?: AbortSignal) {
    return requisitar<TarefaDetalhe>(`/api/v1/tarefas/${id}`, { signal: sinal });
  },

  criar(dados: CriarTarefa) {
    return requisitar<TarefaDetalhe>("/api/v1/tarefas", { method: "POST", body: JSON.stringify(dados) });
  },
};
