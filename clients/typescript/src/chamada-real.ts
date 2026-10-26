// Chamada real à API usando o cliente gerado pelo NSwag (nenhuma URL ou tipo escrito à mão).
// Uso: npm run gerar && npm run chamada-real   (com a API rodando em https://localhost:7001)
import { PortalTarefasClient, type CriarTarefaDto } from "./portal-tarefas-client";

const cliente = new PortalTarefasClient("https://localhost:7001", { fetch: (url, init) => fetch(url, init) });

const pagina = await cliente.listarTarefas(1, 5, "prazo", "asc", "Pendente", undefined, undefined);
console.log(`Página ${pagina.pagina}/${pagina.totalPaginas} – ${pagina.totalItens} tarefa(s) pendente(s)`);
for (const t of pagina.itens ?? []) console.log(` #${t.id} ${t.titulo} (prazo ${t.prazo ?? "-"})`);

const nova: CriarTarefaDto = { titulo: "Criada pelo cliente TypeScript", prioridade: "Media" as never };
const criada = await cliente.criarTarefa(nova);
console.log(`Criada: #${criada.id} versão ${criada.versao}`);
