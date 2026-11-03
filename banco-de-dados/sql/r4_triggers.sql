/* =====================================================================
   Programação de Banco de Dados – Roteiro 4
   Implementação de Regras de Negócio com Triggers em SQL Server
   ===================================================================== */

-- @passo r4_auditoria | Etapa 1 – tabela e trigger de auditoria de preços
USE LojaVirtual;
GO
CREATE TABLE dbo.AuditoriaPrecos (
    AuditoriaID   INT IDENTITY PRIMARY KEY,
    ProdutoID     INT NOT NULL,
    PrecoAnterior DECIMAL(10,2) NOT NULL,
    PrecoNovo     DECIMAL(10,2) NOT NULL,
    DataAlteracao DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UsuarioBanco  SYSNAME   NOT NULL DEFAULT SUSER_SNAME()
);
GO
CREATE OR ALTER TRIGGER dbo.trg_AuditarPrecoProduto
ON dbo.Produtos
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    -- inserted = valores novos; deleted = valores anteriores (podem ser várias linhas)
    INSERT INTO dbo.AuditoriaPrecos (ProdutoID, PrecoAnterior, PrecoNovo)
    SELECT i.ProdutoID, d.Preco, i.Preco
    FROM inserted AS i
    INNER JOIN deleted AS d ON d.ProdutoID = i.ProdutoID
    WHERE i.Preco <> d.Preco;
END;
GO
-- teste 1: só estoque -> nenhuma auditoria
UPDATE dbo.Produtos SET Estoque = Estoque + 5 WHERE ProdutoID = 3;
SELECT COUNT(*) AS AuditoriasAposAlterarEstoque FROM dbo.AuditoriaPrecos;
-- teste 2: preço de um produto
UPDATE dbo.Produtos SET Preco = 339.90 WHERE ProdutoID = 2;
-- teste 3: preço de dois produtos em um único UPDATE
UPDATE dbo.Produtos SET Preco = Preco * 1.10 WHERE ProdutoID IN (4, 7);
SELECT a.AuditoriaID, p.Nome AS Produto, a.PrecoAnterior, a.PrecoNovo,
       CONVERT(VARCHAR(19), a.DataAlteracao, 120) AS DataAlteracao, a.UsuarioBanco
FROM dbo.AuditoriaPrecos AS a INNER JOIN dbo.Produtos AS p ON p.ProdutoID = a.ProdutoID
ORDER BY a.AuditoriaID;
GO

-- @passo r4_preparacao | Etapa 2 – pedidos pendentes para os testes de estoque
USE LojaVirtual;
GO
INSERT INTO dbo.Pedidos (ClienteID, DataPedido, StatusPedido, ValorTotal) VALUES
(3, '2026-10-26 10:00', 'Pendente',  199.80),   -- pedido 6: estoque suficiente
(2, '2026-10-26 10:10', 'Pendente', 1320.00),   -- pedido 7: quantidade maior que o estoque
(6, '2026-10-26 10:20', 'Pendente',  189.90),   -- pedido 8: válido (teste em conjunto)
(5, '2026-10-26 10:30', 'Pendente',  405.79);   -- pedido 9: válido (teste em conjunto)
INSERT INTO dbo.ItensPedido (PedidoID, ProdutoID, Quantidade, PrecoUnitario) VALUES
(6, 3,  2,  99.90),
(7, 4, 10, 132.00),
(8, 1,  1, 189.90),
(9, 2,  1, 339.90),
(9, 7,  1,  65.89);
GO
SELECT p.PedidoID, p.StatusPedido, pr.Nome AS Produto, ip.Quantidade, pr.Estoque AS EstoqueAtual
FROM dbo.Pedidos AS p
INNER JOIN dbo.ItensPedido AS ip ON ip.PedidoID = p.PedidoID
INNER JOIN dbo.Produtos    AS pr ON pr.ProdutoID = ip.ProdutoID
WHERE p.StatusPedido = 'Pendente'
ORDER BY p.PedidoID;
GO

-- @passo r4_trigger_estoque | Etapas 3 e 4 – validação e baixa automática do estoque
USE LojaVirtual;
GO
CREATE OR ALTER TRIGGER dbo.trg_ValidarEstoquePedido
ON dbo.Pedidos
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- só interessa quem mudou de "diferente de Pago" para "Pago"
    IF NOT EXISTS (SELECT 1 FROM inserted AS i INNER JOIN deleted AS d ON d.PedidoID = i.PedidoID
                   WHERE i.StatusPedido = 'Pago' AND d.StatusPedido <> 'Pago')
        RETURN;

    -- validação: soma das quantidades por produto x estoque disponível
    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        INNER JOIN deleted AS d      ON d.PedidoID = i.PedidoID
        INNER JOIN dbo.ItensPedido AS ip ON ip.PedidoID = i.PedidoID
        INNER JOIN dbo.Produtos    AS p  ON p.ProdutoID = ip.ProdutoID
        WHERE i.StatusPedido = 'Pago' AND d.StatusPedido <> 'Pago'
        GROUP BY ip.ProdutoID, p.Estoque
        HAVING SUM(ip.Quantidade) > p.Estoque
    )
        THROW 50001, 'Não há estoque suficiente para confirmar o pedido.', 1;

    -- baixa orientada a conjuntos: funciona para 1 ou N pedidos no mesmo UPDATE
    ;WITH ProdutosVendidos AS (
        SELECT ip.ProdutoID, SUM(ip.Quantidade) AS QuantidadeVendida
        FROM inserted AS i
        INNER JOIN deleted AS d      ON d.PedidoID = i.PedidoID
        INNER JOIN dbo.ItensPedido AS ip ON ip.PedidoID = i.PedidoID
        WHERE i.StatusPedido = 'Pago' AND d.StatusPedido <> 'Pago'
        GROUP BY ip.ProdutoID
    )
    UPDATE p
    SET p.Estoque = p.Estoque - pv.QuantidadeVendida
    FROM dbo.Produtos AS p
    INNER JOIN ProdutosVendidos AS pv ON pv.ProdutoID = p.ProdutoID;
END;
GO
SELECT name AS TriggerCriada, OBJECT_NAME(parent_id) AS Tabela, is_disabled FROM sys.triggers ORDER BY name;
GO

-- @passo r4_teste_valido | Teste 1 e 2 – pagamento válido e repetição sem nova baixa
USE LojaVirtual;
GO
SELECT 'antes' AS Momento, ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE ProdutoID = 3;
UPDATE dbo.Pedidos SET StatusPedido = 'Pago' WHERE PedidoID = 6;
SELECT 'depois do pagamento' AS Momento, ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE ProdutoID = 3;
UPDATE dbo.Pedidos SET StatusPedido = 'Pago' WHERE PedidoID = 6;    -- já estava pago
SELECT 'depois da repetição' AS Momento, ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE ProdutoID = 3;
GO

-- @passo r4_teste_multiplo | Teste 3 – dois pedidos pagos em um único UPDATE
USE LojaVirtual;
GO
SELECT 'antes' AS Momento, ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE ProdutoID IN (1, 2, 7);
UPDATE dbo.Pedidos SET StatusPedido = 'Pago' WHERE PedidoID IN (8, 9);
SELECT @@ROWCOUNT AS PedidosAtualizados;
SELECT 'depois' AS Momento, ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE ProdutoID IN (1, 2, 7);
GO

-- @passo r4_teste_invalido | Teste 4 – pedido sem estoque suficiente
USE LojaVirtual;
GO
UPDATE dbo.Pedidos SET StatusPedido = 'Pago' WHERE PedidoID = 7;
GO
SELECT p.PedidoID, p.StatusPedido, pr.Nome AS Produto, ip.Quantidade AS Solicitado, pr.Estoque
FROM dbo.Pedidos AS p
INNER JOIN dbo.ItensPedido AS ip ON ip.PedidoID = p.PedidoID
INNER JOIN dbo.Produtos    AS pr ON pr.ProdutoID = ip.ProdutoID
WHERE p.PedidoID = 7;
GO

-- @passo r4_administracao | Teste 5 – desabilitar e reabilitar a trigger
USE LojaVirtual;
GO
DISABLE TRIGGER dbo.trg_ValidarEstoquePedido ON dbo.Pedidos;
SELECT name, is_disabled FROM sys.triggers WHERE name = 'trg_ValidarEstoquePedido';
ENABLE TRIGGER dbo.trg_ValidarEstoquePedido ON dbo.Pedidos;
SELECT name, is_disabled FROM sys.triggers ORDER BY name;   -- ambas habilitadas ao final
GO
