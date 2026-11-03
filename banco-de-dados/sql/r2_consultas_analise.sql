/* =====================================================================
   Programação de Banco de Dados – Roteiro 2
   Consultas SQL para Análise de Clientes, Produtos e Pedidos
   ===================================================================== */

-- @passo r2_dados | Dados adicionais coerentes (sem apagar os do Roteiro 1)
USE LojaVirtual;
GO
INSERT INTO dbo.Clientes (Nome, Email, Cidade, Estado, DataCadastro) VALUES
('Diego Rocha',    'diego.rocha@email.com',    'Belo Horizonte', 'MG', '2026-09-20'),
('Elisa Fernandes','elisa.fernandes@email.com','Santos',         'SP', '2026-10-02');

INSERT INTO dbo.Pedidos (ClienteID, DataPedido, StatusPedido, ValorTotal) VALUES
(3, '2026-10-12 16:20', 'Enviado', 119.80),
(6, '2026-10-22 11:00', 'Entregue', 349.90);   -- Diego (ID 6: o IDENTITY não reaproveita números)

INSERT INTO dbo.ItensPedido (PedidoID, ProdutoID, Quantidade, PrecoUnitario) VALUES
(4, 7, 2,  59.90),
(5, 2, 1, 349.90);
GO
SELECT COUNT(*) AS Clientes FROM dbo.Clientes;
GO

-- @passo r2_filtros | Etapa 1 – filtros e ordenação
USE LojaVirtual;
GO
-- clientes de SP em ordem alfabética
SELECT Nome, Cidade, Estado FROM dbo.Clientes WHERE Estado = 'SP' ORDER BY Nome;
-- clientes cujo nome contém um trecho
SELECT Nome, Email FROM dbo.Clientes WHERE Nome LIKE '%ar%';
-- produtos ativos com preço entre dois valores
SELECT ProdutoID, Nome, Preco, Estoque FROM dbo.Produtos
WHERE Ativo = 1 AND Preco BETWEEN 50.00 AND 500.00 ORDER BY Preco DESC;
-- estoque abaixo de dez unidades
SELECT Nome, Estoque FROM dbo.Produtos WHERE Estoque < 10 ORDER BY Estoque;
-- status diferentes existentes
SELECT DISTINCT StatusPedido FROM dbo.Pedidos;
GO

-- @passo r2_joins | Etapa 2 – consultas com relacionamentos
USE LojaVirtual;
GO
-- INNER JOIN: só pedidos que têm cliente correspondente
SELECT p.PedidoID, c.Nome AS Cliente, c.Email, p.StatusPedido, p.ValorTotal
FROM dbo.Pedidos AS p
INNER JOIN dbo.Clientes AS c ON c.ClienteID = p.ClienteID
ORDER BY p.PedidoID;
-- LEFT JOIN: todos os clientes, inclusive quem ainda não comprou
SELECT c.Nome AS Cliente, p.PedidoID, p.DataPedido, p.StatusPedido, p.ValorTotal
FROM dbo.Clientes AS c
LEFT JOIN dbo.Pedidos AS p ON p.ClienteID = c.ClienteID
ORDER BY c.Nome, p.DataPedido;
GO

-- @passo r2_agregacoes | Etapa 3 – indicadores com funções agregadas
USE LojaVirtual;
GO
SELECT (SELECT COUNT(*) FROM dbo.Clientes) AS Clientes,
       (SELECT COUNT(*) FROM dbo.Produtos) AS Produtos,
       (SELECT COUNT(*) FROM dbo.Pedidos)  AS Pedidos;

SELECT MIN(Preco) AS MenorPreco, MAX(Preco) AS MaiorPreco, CAST(AVG(Preco) AS DECIMAL(10,2)) AS PrecoMedio
FROM dbo.Produtos WHERE Ativo = 1;

SELECT c.Nome AS Categoria, COUNT(*) AS Produtos, CAST(AVG(p.Preco) AS DECIMAL(10,2)) AS PrecoMedio
FROM dbo.Produtos AS p INNER JOIN dbo.Categorias AS c ON c.CategoriaID = p.CategoriaID
GROUP BY c.Nome ORDER BY Produtos DESC;

-- HAVING filtra os grupos (WHERE filtraria as linhas antes do agrupamento)
SELECT c.Nome AS Categoria, COUNT(*) AS Produtos
FROM dbo.Produtos AS p INNER JOIN dbo.Categorias AS c ON c.CategoriaID = p.CategoriaID
GROUP BY c.Nome HAVING COUNT(*) > 1;
GO

-- @passo r2_subconsultas | Etapa 4 – subconsultas e CASE
USE LojaVirtual;
GO
SELECT ProdutoID, Nome, Preco, Estoque,
       CASE WHEN Estoque < 5  THEN 'Crítico'
            WHEN Estoque < 15 THEN 'Baixo'
            ELSE 'Adequado' END AS SituacaoEstoque
FROM dbo.Produtos
WHERE Preco > (SELECT AVG(Preco) FROM dbo.Produtos WHERE Ativo = 1)
ORDER BY Preco DESC;

SELECT Nome FROM dbo.Clientes AS c
WHERE EXISTS (SELECT 1 FROM dbo.Pedidos AS p WHERE p.ClienteID = c.ClienteID);

SELECT PedidoID, ClienteID, ValorTotal FROM dbo.Pedidos
WHERE ValorTotal = (SELECT MAX(ValorTotal) FROM dbo.Pedidos);

SELECT Nome, Estoque,
       CASE WHEN Estoque < 5 THEN 'Crítico' WHEN Estoque < 15 THEN 'Baixo' ELSE 'Adequado' END AS SituacaoEstoque
FROM dbo.Produtos ORDER BY Estoque;
GO

-- @passo r2_sintese | Etapa 5 – síntese gerencial
USE LojaVirtual;
GO
SELECT c.ClienteID, c.Nome,
       COUNT(p.PedidoID)               AS QuantidadePedidos,
       COALESCE(SUM(p.ValorTotal), 0)  AS TotalComprado,
       CAST(COALESCE(AVG(p.ValorTotal), 0) AS DECIMAL(10,2)) AS ValorMedio
FROM dbo.Clientes AS c
LEFT JOIN dbo.Pedidos AS p ON p.ClienteID = c.ClienteID
GROUP BY c.ClienteID, c.Nome
ORDER BY TotalComprado DESC;
GO
