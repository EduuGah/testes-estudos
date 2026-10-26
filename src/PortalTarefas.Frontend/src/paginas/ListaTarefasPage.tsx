import { useSearchParams, Link } from "react-router-dom";
import { tarefasApi } from "../api/tarefasApi";
import { useRequisicao } from "../api/useRequisicao";
import type { FiltrosTarefas, Situacao } from "../api/tipos";
import { CartaoTarefa } from "../componentes/CartaoTarefa";
import { Carregando, MensagemErro, Ocioso, Vazio } from "../componentes/Estados";
import { Paginacao } from "../componentes/Paginacao";

const ORDENACOES = ["id", "titulo", "prazo", "prioridade", "situacao"] as const;

/** Lê os filtros da URL: a URL é a fonte da verdade (link compartilhável, voltar/avançar funcionam). */
function lerFiltros(p: URLSearchParams): FiltrosTarefas {
  const ordenarPor = p.get("ordenarPor") as FiltrosTarefas["ordenarPor"] | null;
  return {
    pagina: Math.max(1, Number(p.get("pagina")) || 1),
    tamanho: 5,
    ordenarPor: ordenarPor && ORDENACOES.includes(ordenarPor) ? ordenarPor : "id",
    direcao: p.get("direcao") === "desc" ? "desc" : "asc",
    situacao: (p.get("situacao") as Situacao) || undefined,
    busca: p.get("busca") || undefined,
  };
}

export function ListaTarefasPage() {
  const [params, setParams] = useSearchParams();
  const filtros = lerFiltros(params);

  const [estado, recarregar] = useRequisicao(
    (sinal) => tarefasApi.listar(filtros, sinal),
    [params.toString()],
    (pagina) => pagina.itens.length === 0,
  );

  function atualizar(mudancas: Record<string, string | undefined>, voltarParaPrimeira = true) {
    const novo = new URLSearchParams(params);
    for (const [k, v] of Object.entries(mudancas)) (v ? novo.set(k, v) : novo.delete(k));
    if (voltarParaPrimeira) novo.delete("pagina");
    setParams(novo);
  }

  return (
    <section>
      <div className="cabecalho-pagina">
        <h1>Tarefas</h1>
        <Link className="botao" to="/tarefas/nova">+ Nova tarefa</Link>
      </div>

      <form className="filtros" onSubmit={(e) => { e.preventDefault(); atualizar({ busca: new FormData(e.currentTarget).get("busca")?.toString() }); }}>
        <label className="sr-only" htmlFor="busca">Buscar</label>
        <input id="busca" name="busca" defaultValue={filtros.busca} placeholder="Buscar pelo título" />
        <select aria-label="Situação" value={filtros.situacao ?? ""} onChange={(e) => atualizar({ situacao: e.target.value || undefined })}>
          <option value="">Todas as situações</option>
          <option value="Pendente">Pendente</option>
          <option value="EmAndamento">Em andamento</option>
          <option value="Concluida">Concluída</option>
        </select>
        <select aria-label="Ordenar por" value={filtros.ordenarPor} onChange={(e) => atualizar({ ordenarPor: e.target.value })}>
          {ORDENACOES.map((o) => <option key={o} value={o}>Ordenar por {o}</option>)}
        </select>
        <select aria-label="Direção" value={filtros.direcao} onChange={(e) => atualizar({ direcao: e.target.value })}>
          <option value="asc">Crescente</option>
          <option value="desc">Decrescente</option>
        </select>
        <button type="submit">Buscar</button>
      </form>

      {estado.tipo === "idle" && <Ocioso />}
      {estado.tipo === "carregando" && <Carregando />}
      {estado.tipo === "vazio" && <Vazio texto="Nenhuma tarefa encontrada com esses filtros." />}
      {estado.tipo === "erro" && <MensagemErro erro={estado.erro} aoTentarNovamente={recarregar} />}
      {estado.tipo === "sucesso" && (
        <>
          <p className="resumo">{estado.dados.totalItens} tarefa(s) encontrada(s)</p>
          {estado.dados.itens.map((t) => <CartaoTarefa key={t.id} tarefa={t} />)}
          <Paginacao
            pagina={estado.dados.pagina}
            totalPaginas={estado.dados.totalPaginas}
            aoMudar={(p) => atualizar({ pagina: String(p) }, false)}
          />
        </>
      )}
    </section>
  );
}
