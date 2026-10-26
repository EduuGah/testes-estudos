# Unidade 5 – Front-end React

Versões registradas: Node.js 22 LTS, npm 10, Vite 6, React 18.3, React Router 7, TypeScript 5.6, Vitest 3.

```bash
cd src/PortalTarefas.Frontend
npm ci
npm run dev        # http://localhost:5173 (proxy /api -> https://localhost:7001)
npm test           # testes da camada HTTP
npm run build      # build de produção em dist/
```

## Decisões de integração
* **Proxy em vez de CORS no desenvolvimento**: o navegador só conversa com a origem do Vite; o proxy
  encaminha `/api`. A política CORS `FrontendLocal` (origem explícita http://localhost:5173, sem curinga)
  continua no back-end para chamadas diretas.
* **Camada única de acesso** (`src/api/cliente.ts` + `tarefasApi.ts`): nenhum componente chama `fetch` diretamente.
  Falha de rede, status de erro e Problem Details viram um `ErroApi` normalizado.
* **Estados explícitos** (`useRequisicao`): idle, carregando, vazio, sucesso e erro.
* **Respostas obsoletas**: cada nova busca aborta a anterior com `AbortController`.
* **URL como fonte dos filtros**: `?situacao=Pendente&ordenarPor=prazo&pagina=2` pode ser copiado e reaberto.
* **Simulação**: `VITE_SIMULAR_ATRASO_MS=3000` ou `VITE_SIMULAR_FALHA=true` em `.env.development.local`.
