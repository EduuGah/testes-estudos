// Capturas de tela reais da aplicação em execução (Playwright/Chromium).
// Uso: node telas.mjs <fase>   fases: mvc | spa | container
import { chromium } from "playwright";

const fase = process.argv[2];
const SENHA = process.env.SEED_PWD;
const API = "https://localhost:7001";
const SPA = "http://localhost:5173/app";
const OUT = "../evidencias/telas/";

const b = await chromium.launch();
const novoContexto = () => b.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1200, height: 760 }, locale: "pt-BR" });
const shot = (p, n) => p.screenshot({ path: OUT + n + ".png", fullPage: true }).then(() => console.log("tela", n));

async function entrarMvc(p, email) {
  await p.goto(API + "/Conta/Entrar");
  await p.fill("#Email", email);
  await p.fill("#Senha", SENHA);
  await p.click("button[type=submit]");
  await p.waitForLoadState("networkidle");
}

if (fase === "mvc") {
  const ctx = await novoContexto();
  const p = await ctx.newPage();
  // Unidade 6: página protegida redireciona para o login
  await p.goto(API + "/Tarefas");
  await shot(p, "u6_mvc_login_redirecionado");
  await entrarMvc(p, "admin@portal.local");
  await shot(p, "u3_lista_do_banco");
  // Unidade 3: operação transacional (situação + auditoria)
  await p.goto(API + "/Tarefas/Details/3");
  await p.click("button[value=Concluida]");
  await p.waitForLoadState("networkidle");
  await shot(p, "u3_transacao_situacao_alterada");
  // Unidade 3: conflito de concorrência com duas abas na mesma tarefa
  const a = await ctx.newPage(), c = await ctx.newPage();
  await a.goto(API + "/Tarefas/Edit/4");
  await c.goto(API + "/Tarefas/Edit/4");
  await a.fill("#Titulo", "Montar telas Razor (revisado na aba A)");
  await a.click("button[type=submit]");
  await a.waitForLoadState("networkidle");
  await shot(a, "u3_concorrencia_aba_a_salvou");
  await c.selectOption("#Situacao", "EmAndamento");
  await c.click("button[type=submit]");
  await c.waitForLoadState("networkidle");
  await shot(c, "u3_concorrencia_aba_b_conflito");
  // Unidade 6: usuário de outra equipe tentando editar
  const ctx2 = await novoContexto();
  const p2 = await ctx2.newPage();
  await entrarMvc(p2, "bruno@portal.local");
  await p2.goto(API + "/Tarefas/Edit/1");
  await p2.waitForLoadState("networkidle");
  await shot(p2, "u6_mvc_acesso_negado");
  // Unidade 4: interface interativa da OpenAPI (Scalar)
  const p3 = await ctx.newPage();
  await p3.goto(API + "/scalar/v1");
  await p3.waitForTimeout(4000);
  await p3.screenshot({ path: OUT + "u4_scalar.png" });
  console.log("tela u4_scalar");
}

if (fase === "spa") {
  const ctx = await novoContexto();
  const p = await ctx.newPage();
  await p.goto(SPA + "/?situacao=Pendente&ordenarPor=prazo");
  await p.waitForURL(/entrar/);
  await shot(p, "u6_spa_redirecionado_para_login");
  await p.fill("#email", "ana@portal.local");
  await p.fill("#senha", "senha-errada");
  await p.click("button[type=submit]");
  await p.waitForSelector("[role=alert]");
  await shot(p, "u6_spa_login_invalido");
  await p.fill("#senha", SENHA);
  await p.click("button[type=submit]");
  await p.waitForSelector(".cartao, .estado.vazio");
  await shot(p, "u5_lista_filtros_na_url");  // voltou para a URL pretendida, com filtros
  await p.goto(SPA + "/?pagina=2");
  await p.waitForSelector(".cartao");
  await shot(p, "u5_lista_pagina_2");
  await p.goto(SPA + "/?busca=inexistente-xyz");
  await p.waitForSelector(".estado.vazio");
  await shot(p, "u5_lista_vazia");
  await p.goto(SPA + "/tarefas/2");
  await p.waitForSelector("dl.detalhes");
  await shot(p, "u5_detalhe");
  await p.goto(SPA + "/tarefas/99999");
  await p.waitForSelector("[role=alert]");
  await shot(p, "u5_detalhe_404");
  await p.goto(SPA + "/tarefas/nova");
  await p.fill("#titulo", "ab");
  await p.click("button[type=submit]");
  await p.waitForSelector(".erro");
  await shot(p, "u5_formulario_problem_details");
  await p.fill("#titulo", "Tarefa criada pela SPA");
  await p.click("button[type=submit]");
  await p.waitForSelector("dl.detalhes");
  await shot(p, "u5_cadastro_valido_detalhe");
}

if (fase === "container") {
  const ctx = await b.newContext({ viewport: { width: 1200, height: 760 }, locale: "pt-BR" });
  const p = await ctx.newPage();
  await p.goto("http://localhost:8080/app/");
  await p.waitForURL(/entrar/);
  await shot(p, "u8_container_spa_integrada");
}

await b.close();
