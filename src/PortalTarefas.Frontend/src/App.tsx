import { BrowserRouter, Link, Route, Routes } from "react-router-dom";
import { AuthProvider, useAuth } from "./autenticacao/AuthContext";
import { RotaProtegida } from "./autenticacao/RotaProtegida";
import { ListaTarefasPage } from "./paginas/ListaTarefasPage";
import { DetalheTarefaPage } from "./paginas/DetalheTarefaPage";
import { NovaTarefaPage } from "./paginas/NovaTarefaPage";
import { EntrarPage } from "./paginas/EntrarPage";

function Topo() {
  const { usuario, sair, temPapel } = useAuth();
  return (
    <header className="topo">
      <Link to="/" className="marca">Portal de Tarefas</Link>
      <nav>
        <Link to="/">Tarefas</Link>
        {/* Oculta a ação para quem não pode — apenas conveniência; a API valida de novo */}
        {temPapel("Usuario", "Administrador") && <Link to="/tarefas/nova">Nova tarefa</Link>}
        {usuario ? (
          <>
            <span className="usuario">{usuario.nome}</span>
            <button className="link" onClick={() => void sair()}>Sair</button>
          </>
        ) : <Link to="/entrar">Entrar</Link>}
      </nav>
    </header>
  );
}

export default function App() {
  return (
    <BrowserRouter basename={import.meta.env.BASE_URL}>
      <AuthProvider>
        <Topo />
        <main className="conteudo">
          <Routes>
            <Route path="/entrar" element={<EntrarPage />} />
            <Route element={<RotaProtegida />}>
              <Route path="/" element={<ListaTarefasPage />} />
              <Route path="/tarefas/:id" element={<DetalheTarefaPage />} />
            </Route>
            <Route element={<RotaProtegida papeis={["Usuario", "Administrador"]} />}>
              <Route path="/tarefas/nova" element={<NovaTarefaPage />} />
            </Route>
            <Route path="*" element={<p>Página não encontrada.</p>} />
          </Routes>
        </main>
      </AuthProvider>
    </BrowserRouter>
  );
}
