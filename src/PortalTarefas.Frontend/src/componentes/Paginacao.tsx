interface Props { pagina: number; totalPaginas: number; aoMudar: (p: number) => void }

export function Paginacao({ pagina, totalPaginas, aoMudar }: Props) {
  if (totalPaginas <= 1) return null;
  return (
    <nav className="paginacao" aria-label="Paginação">
      <button disabled={pagina <= 1} onClick={() => aoMudar(pagina - 1)}>Anterior</button>
      <span>Página {pagina} de {totalPaginas}</span>
      <button disabled={pagina >= totalPaginas} onClick={() => aoMudar(pagina + 1)}>Próxima</button>
    </nav>
  );
}
