import type { ProblemDetails } from "./tipos";

/** Erro normalizado: a interface trata sempre o mesmo formato, venha de onde vier a falha. */
export class ErroApi extends Error {
  constructor(
    public readonly tipo: "rede" | "http" | "cancelado",
    mensagem: string,
    public readonly status?: number,
    public readonly problema?: ProblemDetails,
  ) {
    super(mensagem);
  }

  /** Erros de validação por campo (400 com "errors"). */
  get errosDeCampo(): Record<string, string[]> {
    return this.problema?.errors ?? {};
  }
}

const baseUrl = import.meta.env.VITE_API_URL ?? "";
const atrasoSimulado = Number(import.meta.env.VITE_SIMULAR_ATRASO_MS ?? 0);
const falhaSimulada = import.meta.env.VITE_SIMULAR_FALHA === "true";

const esperar = (ms: number, sinal?: AbortSignal) =>
  new Promise<void>((resolve, reject) => {
    const t = setTimeout(resolve, ms);
    sinal?.addEventListener("abort", () => { clearTimeout(t); reject(new DOMException("Abortado", "AbortError")); });
  });

/**
 * Função central de acesso à API: monta a URL, envia, e transforma
 * (1) falha de rede, (2) status não 2xx e (3) Problem Details em ErroApi.
 */
export async function requisitar<T>(caminho: string, init: RequestInit = {}): Promise<T> {
  let resposta: Response;
  try {
    if (atrasoSimulado > 0) await esperar(atrasoSimulado, init.signal ?? undefined);
    if (falhaSimulada) throw new TypeError("Falha de rede simulada");
    resposta = await fetch(baseUrl + caminho, {
      ...init,
      credentials: "include",
      headers: { Accept: "application/json", ...(init.body ? { "Content-Type": "application/json" } : {}), ...init.headers },
    });
  } catch (e) {
    if (e instanceof DOMException && e.name === "AbortError") throw new ErroApi("cancelado", "Requisição cancelada.");
    throw new ErroApi("rede", "Não foi possível conectar à API. Verifique sua conexão e tente novamente.");
  }

  if (resposta.ok) {
    return resposta.status === 204 ? (undefined as T) : ((await resposta.json()) as T);
  }

  const tipo = resposta.headers.get("Content-Type") ?? "";
  if (tipo.includes("application/problem+json")) {
    const problema = (await resposta.json()) as ProblemDetails;
    throw new ErroApi("http", problema.detail ?? problema.title ?? `Erro ${resposta.status}`, resposta.status, problema);
  }
  throw new ErroApi("http", `A API respondeu com status ${resposta.status}.`, resposta.status);
}
