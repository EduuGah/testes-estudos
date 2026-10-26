import { afterEach, describe, expect, it, vi } from "vitest";
import { ErroApi, requisitar } from "./cliente";

const resposta = (status: number, corpo: unknown, tipo = "application/json") =>
  new Response(JSON.stringify(corpo), { status, headers: { "Content-Type": tipo } });

describe("requisitar", () => {
  afterEach(() => vi.restoreAllMocks());

  it("devolve os dados tipados em caso de sucesso", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(resposta(200, { id: 1 }));
    await expect(requisitar<{ id: number }>("/api/v1/tarefas/1")).resolves.toEqual({ id: 1 });
  });

  it("normaliza Problem Details com erros por campo", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      resposta(400, { title: "Dados inválidos", status: 400, errors: { titulo: ["Obrigatório"] } }, "application/problem+json"));
    const erro = (await requisitar("/x").catch((e: unknown) => e)) as ErroApi;
    expect(erro).toBeInstanceOf(ErroApi);
    expect(erro.tipo).toBe("http");
    expect(erro.status).toBe(400);
    expect(erro.errosDeCampo.titulo).toEqual(["Obrigatório"]);
  });

  it("diferencia falha de rede", async () => {
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new TypeError("Failed to fetch"));
    const erro = (await requisitar("/x").catch((e: unknown) => e)) as ErroApi;
    expect(erro.tipo).toBe("rede");
  });

  it("trata status de erro sem Problem Details", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response("falha", { status: 502 }));
    const erro = (await requisitar("/x").catch((e: unknown) => e)) as ErroApi;
    expect(erro.status).toBe(502);
  });
});
