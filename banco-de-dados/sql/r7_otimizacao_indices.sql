/* =====================================================================
   Programação de Banco de Dados – Roteiro 7
   Otimização de Consultas e Índices em uma Loja Virtual
   Com poucas linhas o otimizador sempre lê a tabela inteira; por isso foi
   gerada uma massa de teste (50.000 pedidos históricos) só para esta prática.
   ===================================================================== */

-- @passo r7_massa | Preparação – massa de teste representativa
USE LojaVirtual;
GO
SET NOCOUNT ON;
;WITH n AS (
    SELECT TOP (50000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS k
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT INTO dbo.Pedidos (ClienteID, DataPedido, StatusPedido, ValorTotal)
SELECT CHOOSE(k % 6 + 1, 1, 2, 3, 6, 7, 2),
       DATEADD(MINUTE, (k * 37) % 1440, DATEADD(DAY, (k * 7919) % 1035, CAST('2024-01-01' AS DATETIME2(0)))),
       'Entregue',
       CAST(50 + (k * 31) % 950 AS DECIMAL(10,2))
FROM n;

INSERT INTO dbo.ItensPedido (PedidoID, ProdutoID, Quantidade, PrecoUnitario)
SELECT p.PedidoID, 1 + p.PedidoID % 7, 1, p.ValorTotal
FROM dbo.Pedidos AS p
WHERE p.PedidoID > 10 AND NOT EXISTS (SELECT 1 FROM dbo.ItensPedido i WHERE i.PedidoID = p.PedidoID);

UPDATE STATISTICS dbo.Pedidos;
UPDATE STATISTICS dbo.ItensPedido;
SELECT (SELECT COUNT(*) FROM dbo.Pedidos) AS Pedidos, (SELECT COUNT(*) FROM dbo.ItensPedido) AS ItensPedido;
GO

-- @passo r7_indices_iniciais | Etapa 1 – índices existentes em Pedidos
USE LojaVirtual;
GO
EXEC sys.sp_helpindex 'dbo.Pedidos';
SELECT i.name, i.type_desc, i.is_unique, i.is_disabled
FROM sys.indexes AS i WHERE i.object_id = OBJECT_ID('dbo.Pedidos') ORDER BY i.index_id;
GO

-- @passo r7_base | Etapa 1 – linha de base (plano real + STATISTICS IO/TIME)
USE LojaVirtual;
GO
SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS XML ON;
SELECT PedidoID, ClienteID, DataPedido, StatusPedido, ValorTotal
FROM dbo.Pedidos
WHERE ClienteID = 1 AND DataPedido >= '2026-01-01' AND DataPedido < '2027-01-01';
SET STATISTICS XML OFF; SET STATISTICS TIME OFF; SET STATISTICS IO OFF;
GO

-- @passo r7_sargavel | Etapa 2 – predicado não sargável x sargável (índice começando por DataPedido)
USE LojaVirtual;
GO
-- índice temporário só para a comparação desta etapa
CREATE NONCLUSTERED INDEX IX_Pedidos_Data_Temp ON dbo.Pedidos (DataPedido) INCLUDE (ClienteID, ValorTotal);
GO
-- os dois retornam o mesmo conjunto?
SELECT (SELECT COUNT(*) FROM dbo.Pedidos WHERE YEAR(DataPedido) = 2026) AS LinhasA,
       (SELECT COUNT(*) FROM dbo.Pedidos WHERE DataPedido >= '2026-01-01' AND DataPedido < '2027-01-01') AS LinhasB,
       (SELECT COUNT(*) FROM (SELECT PedidoID FROM dbo.Pedidos WHERE YEAR(DataPedido) = 2026
                              EXCEPT
                              SELECT PedidoID FROM dbo.Pedidos WHERE DataPedido >= '2026-01-01' AND DataPedido < '2027-01-01') d) AS Diferencas;
GO
SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS XML ON;
-- Consulta A: função aplicada à coluna
SELECT PedidoID, ClienteID, DataPedido, ValorTotal FROM dbo.Pedidos WHERE YEAR(DataPedido) = 2026;
-- Consulta B: intervalo sargável
SELECT PedidoID, ClienteID, DataPedido, ValorTotal FROM dbo.Pedidos
WHERE DataPedido >= '2026-01-01' AND DataPedido < '2027-01-01';
SET STATISTICS XML OFF; SET STATISTICS TIME OFF; SET STATISTICS IO OFF;
GO
DROP INDEX IX_Pedidos_Data_Temp ON dbo.Pedidos;
GO

-- @passo r7_indice | Etapa 3 – índice composto para o relatório e nova medição
USE LojaVirtual;
GO
CREATE NONCLUSTERED INDEX IX_Pedidos_Cliente_Data
ON dbo.Pedidos (ClienteID, DataPedido)
INCLUDE (StatusPedido, ValorTotal);
GO
SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS XML ON;
SELECT PedidoID, ClienteID, DataPedido, StatusPedido, ValorTotal
FROM dbo.Pedidos
WHERE ClienteID = 1 AND DataPedido >= '2026-01-01' AND DataPedido < '2027-01-01';
SET STATISTICS XML OFF; SET STATISTICS TIME OFF; SET STATISTICS IO OFF;
GO

-- @passo r7_ordem | Etapa 4 – ordem das colunas-chave e seletividade
USE LojaVirtual;
GO
SET STATISTICS IO ON; SET STATISTICS XML ON;
SELECT PedidoID, DataPedido, ValorTotal FROM dbo.Pedidos WHERE ClienteID = 1;                       -- só ClienteID
SELECT PedidoID, ClienteID, ValorTotal FROM dbo.Pedidos WHERE DataPedido >= '2026-10-01' AND DataPedido < '2026-10-08'; -- só DataPedido
SELECT PedidoID, ValorTotal FROM dbo.Pedidos WHERE ClienteID = 1 AND DataPedido >= '2026-10-01' AND DataPedido < '2026-10-08'; -- ambas
SET STATISTICS XML OFF; SET STATISTICS IO OFF;
GO
SELECT ClienteID, COUNT(*) AS QuantidadePedidos FROM dbo.Pedidos GROUP BY ClienteID ORDER BY QuantidadePedidos DESC;
DBCC SHOW_STATISTICS ('dbo.Pedidos', 'IX_Pedidos_Cliente_Data') WITH STAT_HEADER, DENSITY_VECTOR, NO_INFOMSGS;
GO

-- @passo r7_join | Etapa 5 – relatório de produtos vendidos (junção, agregação e ordenação)
USE LojaVirtual;
GO
EXEC sys.sp_helpindex 'dbo.ItensPedido';
SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS XML ON;
SELECT p.ProdutoID, p.Nome, SUM(ip.Quantidade) AS QuantidadeVendida
FROM dbo.Produtos AS p
INNER JOIN dbo.ItensPedido AS ip ON ip.ProdutoID = p.ProdutoID
GROUP BY p.ProdutoID, p.Nome
ORDER BY QuantidadeVendida DESC;
SET STATISTICS XML OFF; SET STATISTICS TIME OFF; SET STATISTICS IO OFF;
GO
/* Proposta (NÃO executada):
   CREATE NONCLUSTERED INDEX IX_ItensPedido_Produto
   ON dbo.ItensPedido (ProdutoID) INCLUDE (Quantidade);
   Apoia a junção/agrupamento por ProdutoID lendo só duas colunas; em troca, cada INSERT,
   UPDATE de ProdutoID/Quantidade e DELETE em ItensPedido passa a manter mais uma estrutura. */
