import { BrowserRouter, Link, Route, Routes } from "react-router-dom";
import { ListaTarefasPage } from "./paginas/ListaTarefasPage";
import { DetalheTarefaPage } from "./paginas/DetalheTarefaPage";
import { NovaTarefaPage } from "./paginas/NovaTarefaPage";

export default function App() {
  return (
    <BrowserRouter>
      <header className="topo">
        <Link to="/" className="marca">Portal de Tarefas</Link>
        <nav><Link to="/">Tarefas</Link><Link to="/tarefas/nova">Nova tarefa</Link></nav>
      </header>
      <main className="conteudo">
        <Routes>
          <Route path="/" element={<ListaTarefasPage />} />
          <Route path="/tarefas/nova" element={<NovaTarefaPage />} />
          <Route path="/tarefas/:id" element={<DetalheTarefaPage />} />
          <Route path="*" element={<p>Página não encontrada.</p>} />
        </Routes>
      </main>
    </BrowserRouter>
  );
}
