import { useEffect, useState } from "react";
import { ErroApi } from "./cliente";

/** Os cinco estados explícitos de uma requisição de leitura. */
export type EstadoRequisicao<T> =
  | { tipo: "idle" }
  | { tipo: "carregando" }
  | { tipo: "vazio" }
  | { tipo: "sucesso"; dados: T }
  | { tipo: "erro"; erro: ErroApi };

/**
 * Executa a busca sempre que as dependências mudam. A cada nova execução a
 * anterior é cancelada (AbortController), então uma resposta obsoleta nunca
 * sobrescreve a tela atual.
 */
export function useRequisicao<T>(
  buscar: (sinal: AbortSignal) => Promise<T>,
  dependencias: unknown[],
  estaVazio: (dados: T) => boolean = () => false,
): [EstadoRequisicao<T>, () => void] {
  const [estado, setEstado] = useState<EstadoRequisicao<T>>({ tipo: "idle" });
  const [tentativa, setTentativa] = useState(0);

  useEffect(() => {
    const controlador = new AbortController();
    setEstado({ tipo: "carregando" });
    buscar(controlador.signal)
      .then((dados) => setEstado(estaVazio(dados) ? { tipo: "vazio" } : { tipo: "sucesso", dados }))
      .catch((e: unknown) => {
        if (e instanceof ErroApi && e.tipo === "cancelado") return;   // ignorada: já existe outra requisição
        setEstado({ tipo: "erro", erro: e instanceof ErroApi ? e : new ErroApi("rede", String(e)) });
      });
    return () => controlador.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...dependencias, tentativa]);

  return [estado, () => setTentativa((n) => n + 1)];
}
