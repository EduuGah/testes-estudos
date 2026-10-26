import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { ErroApi } from "../api/cliente";
import { tarefasApi } from "../api/tarefasApi";
import type { CriarTarefa, Prioridade } from "../api/tipos";

export function NovaTarefaPage() {
  const navegar = useNavigate();
  const [enviando, setEnviando] = useState(false);
  const [errosCampo, setErrosCampo] = useState<Record<string, string[]>>({});
  const [erroGeral, setErroGeral] = useState<string>();

  async function enviar(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const dados: CriarTarefa = {
      titulo: String(f.get("titulo") ?? ""),
      descricao: String(f.get("descricao") ?? "") || undefined,
      prioridade: f.get("prioridade") as Prioridade,
      prazo: String(f.get("prazo") ?? "") || undefined,
    };
    setEnviando(true); setErrosCampo({}); setErroGeral(undefined);
    try {
      const criada = await tarefasApi.criar(dados);
      // Navegação no cliente: a interface muda sem recarregar a página inteira
      navegar(`/tarefas/${criada.id}`, { state: { mensagem: "Tarefa criada." } });
    } catch (erro) {
      if (erro instanceof ErroApi && erro.status === 400) setErrosCampo(erro.errosDeCampo);
      else setErroGeral(erro instanceof Error ? erro.message : "Erro inesperado.");
    } finally {
      setEnviando(false);
    }
  }

  const erro = (campo: string) =>
    errosCampo[campo]?.map((m) => <span key={m} className="erro" id={`erro-${campo}`}>{m}</span>);

  return (
    <section>
      <h1>Nova tarefa</h1>
      {erroGeral && <div className="estado erro" role="alert">{erroGeral}</div>}
      <form className="formulario" onSubmit={enviar} noValidate>
        <div className="campo">
          <label htmlFor="titulo">Título</label>
          <input id="titulo" name="titulo" aria-invalid={!!errosCampo.titulo} aria-describedby="erro-titulo" />
          {erro("titulo")}
        </div>
        <div className="campo">
          <label htmlFor="descricao">Descrição</label>
          <textarea id="descricao" name="descricao" rows={3} />
          {erro("descricao")}
        </div>
        <div className="linha">
          <div className="campo">
            <label htmlFor="prioridade">Prioridade</label>
            <select id="prioridade" name="prioridade" defaultValue="Media">
              <option value="Baixa">Baixa</option><option value="Media">Média</option><option value="Alta">Alta</option>
            </select>
          </div>
          <div className="campo">
            <label htmlFor="prazo">Prazo</label>
            <input id="prazo" name="prazo" type="date" />
            {erro("prazo")}
          </div>
        </div>
        <button type="submit" disabled={enviando}>{enviando ? "Salvando..." : "Salvar"}</button>
      </form>
    </section>
  );
}
