import { Link, useParams } from "react-router-dom";
import { tarefasApi } from "../api/tarefasApi";
import { useRequisicao } from "../api/useRequisicao";
import { Carregando, MensagemErro } from "../componentes/Estados";

export function DetalheTarefaPage() {
  const { id } = useParams();
  const idNumerico = Number(id);
  const [estado, recarregar] = useRequisicao((sinal) => tarefasApi.obter(idNumerico, sinal), [idNumerico]);

  if (!Number.isInteger(idNumerico) || idNumerico < 1) return <NaoEncontrada id={id} />;
  if (estado.tipo === "idle" || estado.tipo === "carregando") return <Carregando texto="Carregando tarefa..." />;
  if (estado.tipo === "erro") {
    return estado.erro.status === 404 ? <NaoEncontrada id={id} /> : <MensagemErro erro={estado.erro} aoTentarNovamente={recarregar} />;
  }
  if (estado.tipo !== "sucesso") return null;

  const t = estado.dados;
  return (
    <section>
      <h1>{t.titulo}</h1>
      <dl className="detalhes">
        <dt>Situação</dt><dd>{t.situacao}</dd>
        <dt>Prioridade</dt><dd>{t.prioridade}</dd>
        <dt>Prazo</dt><dd>{t.prazo ? new Date(t.prazo + "T00:00:00").toLocaleDateString("pt-BR") : "sem prazo"}</dd>
        <dt>Descrição</dt><dd>{t.descricao ?? "—"}</dd>
        <dt>Criada em</dt><dd>{new Date(t.criadaEmUtc).toLocaleString("pt-BR")}</dd>
      </dl>
      <Link to="/">← Voltar para a lista</Link>
    </section>
  );
}

function NaoEncontrada({ id }: { id?: string }) {
  return (
    <section className="estado erro" role="alert">
      <h1>Tarefa não encontrada</h1>
      <p>Não existe tarefa com o identificador {id}.</p>
      <Link to="/">Voltar para a lista</Link>
    </section>
  );
}
