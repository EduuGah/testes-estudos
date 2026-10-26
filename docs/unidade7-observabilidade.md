# Unidade 7 – Observabilidade e resiliência

## Sinais
| Sinal | Onde | Exemplo |
|---|---|---|
| Log estruturado | `ILogger` com propriedades nomeadas, escopo `CorrelationId` | `Tarefa {TarefaId} criada em {Duracao} ms` |
| Métrica de negócio | `portal.tarefas.criadas` (counter, dimensão `prioridade`: 3 valores) | |
| Métrica técnica | `portal.dicas.duracao` (histograma em ms, dimensão `resultado`: sucesso, falha ou circuito_aberto) | |
| Trace | ASP.NET Core + HttpClient + span `Tarefas.Criar` e `ServicoDicas.ObterDica` | tag `correlation.id` |
| Saúde | `/health/live` (processo) e `/health/ready` (banco + serviço de dicas) | |

Exportador: console (OpenTelemetry.Exporter.Console). Em produção, trocar por OTLP para um coletor.

## Roteiro de reprodução (Development)
```bash
# Falha transitória: 2 de cada 3 chamadas dão 503 → o retry resolve sem o usuário perceber
curl -X POST https://localhost:7001/simulador/dicas/modo/transitorio
curl https://localhost:7001/api/v1/dica            # (logado) → dica normal; logs mostram as tentativas

# Indisponível: circuito abre após 6 chamadas com 50% de falha → fallback imediato
curl -X POST https://localhost:7001/simulador/dicas/modo/fora
curl https://localhost:7001/health/ready           # 503 {"status":"Unhealthy","verificacoes":{"banco":"Healthy","servico-dicas":"Unhealthy"}}
curl https://localhost:7001/health/live            # 200 Healthy — o processo continua vivo

# Rate limiting: 6º POST em 30 s → 429 com Retry-After
# Output cache: GET /api/publico/estatisticas duas vezes → mesmo "geradoEmUtc"; após criar tarefa → valor novo
```

## Alerta conceitual (extensão)
| Alerta | Limiar | Ação esperada |
|---|---|---|
| Dependência de dicas degradada | `portal.dicas.duracao{resultado="circuito_aberto"}` > 0 por 5 min | Verificar status do provedor; a tela segue com o fallback |
| Readiness falhando | `/health/ready` ≠ 200 em 3 verificações seguidas | Tirar a instância do balanceador e checar o banco |
| Excesso de 429 no login | > 20 rejeições/min | Possível força bruta: revisar IPs e bloqueios |

Testes locais de carga não comprovam capacidade de produção: servem só para demonstrar o comportamento das políticas.
