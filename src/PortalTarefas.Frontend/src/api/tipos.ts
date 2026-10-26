// Tipos espelham o contrato da API v1 (DTOs), nunca a entidade do banco.
export type Prioridade = "Baixa" | "Media" | "Alta";
export type Situacao = "Pendente" | "EmAndamento" | "Concluida";

export interface TarefaResumo {
  id: number;
  titulo: string;
  prioridade: Prioridade;
  prazo: string | null;
  situacao: Situacao;
  vencida: boolean;
}

export interface TarefaDetalhe {
  id: number;
  titulo: string;
  descricao: string | null;
  prioridade: Prioridade;
  prazo: string | null;
  situacao: Situacao;
  criadaEmUtc: string;
  atualizadaEmUtc: string | null;
  versao: string;
}

export interface Pagina<T> {
  itens: T[];
  pagina: number;
  tamanho: number;
  totalItens: number;
  totalPaginas: number;
}

export interface CriarTarefa {
  titulo: string;
  descricao?: string;
  prioridade: Prioridade;
  prazo?: string;
}

export interface FiltrosTarefas {
  pagina: number;
  tamanho: number;
  ordenarPor: "id" | "titulo" | "prazo" | "prioridade" | "situacao";
  direcao: "asc" | "desc";
  situacao?: Situacao;
  busca?: string;
}

/** Problem Details (RFC 9457) devolvido pela API. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
