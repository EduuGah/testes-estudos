// Chamada real à API usando o cliente gerado pelo NSwag (nenhuma URL ou tipo escrito à mão).
// Uso: npm run gerar && npm run chamada-real
// Variáveis: PORTAL_API_URL (padrão https://localhost:7001), PORTAL_EMAIL e PORTAL_SENHA (usuário de teste).
import { PortalTarefasClient, Prioridade, type CriarTarefaDto } from "./portal-tarefas-client";

const base = process.env.PORTAL_API_URL ?? "https://localhost:7001";

// Desde a Unidade 6 a API exige login por cookie: entra uma vez e reaproveita o cookie nas chamadas do cliente gerado.
const login = await fetch(`${base}/api/auth/login`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ email: process.env.PORTAL_EMAIL, senha: process.env.PORTAL_SENHA }),
});
if (!login.ok) throw new Error(`Login falhou: ${login.status}`);
const cookie = login.headers.getSetCookie().map((c) => c.split(";")[0]).join("; ");

const cliente = new PortalTarefasClient(base, {
  fetch: (url, init) => fetch(url, { ...init, headers: { ...(init?.headers as Record<string, string>), Cookie: cookie } }),
});

const pagina = await cliente.listarTarefas(1, 5, "prazo", "asc", "Pendente", undefined, undefined);
console.log(`Página ${pagina.pagina}/${pagina.totalPaginas} – ${pagina.totalItens} tarefa(s) pendente(s)`);
for (const t of pagina.itens ?? []) console.log(` #${t.id} ${t.titulo} (prazo ${t.prazo ?? "-"})`);

const nova: CriarTarefaDto = { titulo: "Criada pelo cliente TypeScript", prioridade: Prioridade.Media };
const criada = await cliente.criarTarefa(nova);
console.log(`Criada: #${criada.id} versão ${criada.versao}`);
