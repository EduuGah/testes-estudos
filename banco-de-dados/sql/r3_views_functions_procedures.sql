/* =====================================================================
   Programação de Banco de Dados – Roteiro 3
   Automação de Operações com Procedures, Functions e Views
   ===================================================================== */

-- @passo r3_views | Etapa 1 – views
USE LojaVirtual;
GO
CREATE OR ALTER VIEW dbo.vw_ProdutosAtivos
AS
SELECT p.ProdutoID, p.Nome AS Produto, c.Nome AS Categoria, p.Preco, p.Estoque
FROM dbo.Produtos AS p
INNER JOIN dbo.Categorias AS c ON c.CategoriaID = p.CategoriaID
WHERE p.Ativo = 1;
GO
CREATE OR ALTER VIEW dbo.vw_ResumoPedidos
AS
SELECT p.PedidoID, p.DataPedido, p.StatusPedido,
       c.ClienteID, c.Nome AS Cliente,
       COUNT(ip.ItemPedidoID)               AS QuantidadeItens,
       SUM(ip.Quantidade * ip.PrecoUnitario) AS ValorCalculado
FROM dbo.Pedidos AS p
INNER JOIN dbo.Clientes    AS c  ON c.ClienteID = p.ClienteID
INNER JOIN dbo.ItensPedido AS ip ON ip.PedidoID = p.PedidoID
GROUP BY p.PedidoID, p.DataPedido, p.StatusPedido, c.ClienteID, c.Nome;
GO
CREATE OR ALTER VIEW dbo.vw_ClientesSemPedidos
AS
SELECT c.ClienteID, c.Nome, c.Email, c.Cidade
FROM dbo.Clientes AS c
LEFT JOIN dbo.Pedidos AS p ON p.ClienteID = c.ClienteID
WHERE p.PedidoID IS NULL;
GO
SELECT * FROM dbo.vw_ProdutosAtivos WHERE Categoria = 'Eletrônicos' ORDER BY Preco DESC;
SELECT * FROM dbo.vw_ResumoPedidos ORDER BY ValorCalculado DESC;
SELECT * FROM dbo.vw_ClientesSemPedidos;
GO

-- @passo r3_functions | Etapa 2 – função escalar e função de tabela
USE LojaVirtual;
GO
CREATE OR ALTER FUNCTION dbo.fn_CalcularSubtotal (@Quantidade INT, @PrecoUnitario DECIMAL(10,2))
RETURNS DECIMAL(12,2)
AS
BEGIN
    RETURN @Quantidade * @PrecoUnitario;
END;
GO
CREATE OR ALTER FUNCTION dbo.fn_PedidosPorCliente (@ClienteID INT)
RETURNS TABLE
AS
RETURN
(
    SELECT PedidoID, DataPedido, StatusPedido, ValorTotal
    FROM dbo.Pedidos
    WHERE ClienteID = @ClienteID
);
GO
SELECT dbo.fn_CalcularSubtotal(3, 19.90) AS TesteDireto;
SELECT ItemPedidoID, PedidoID, Quantidade, PrecoUnitario,
       dbo.fn_CalcularSubtotal(Quantidade, PrecoUnitario) AS Subtotal
FROM dbo.ItensPedido;
SELECT * FROM dbo.fn_PedidosPorCliente(1);
SELECT * FROM dbo.fn_PedidosPorCliente(7);   -- cliente sem pedidos: tabela vazia
GO

-- @passo r3_proc_consulta | Etapa 3 – procedure de consulta com parâmetro opcional
USE LojaVirtual;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ListarPedidosCliente
    @ClienteID    INT,
    @StatusPedido VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PedidoID, DataPedido, StatusPedido, Cliente, QuantidadeItens, ValorCalculado
    FROM dbo.vw_ResumoPedidos
    WHERE ClienteID = @ClienteID
      AND (@StatusPedido IS NULL OR StatusPedido = @StatusPedido)
    ORDER BY DataPedido DESC;
END;
GO
EXEC dbo.sp_ListarPedidosCliente @ClienteID = 1;
EXEC dbo.sp_ListarPedidosCliente @ClienteID = 1, @StatusPedido = 'Pendente';
GO

-- @passo r3_proc_preco | Etapa 4 – procedure de atualização com OUTPUT
USE LojaVirtual;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AtualizarPrecoProduto
    @ProdutoID      INT,
    @NovoPreco      DECIMAL(10,2),
    @LinhasAfetadas INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @NovoPreco <= 0
        THROW 50001, 'O preço deve ser maior que zero.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Produtos WHERE ProdutoID = @ProdutoID)
        THROW 50002, 'Produto não encontrado.', 1;

    UPDATE dbo.Produtos SET Preco = @NovoPreco WHERE ProdutoID = @ProdutoID;
    SET @LinhasAfetadas = @@ROWCOUNT;
END;
GO
-- teste 1: produto existente (integração: view -> procedure -> view)
SELECT ProdutoID, Produto, Preco FROM dbo.vw_ProdutosAtivos WHERE ProdutoID = 1;
DECLARE @Linhas INT;
EXEC dbo.sp_AtualizarPrecoProduto @ProdutoID = 1, @NovoPreco = 189.90, @LinhasAfetadas = @Linhas OUTPUT;
SELECT @Linhas AS LinhasAfetadas;
SELECT ProdutoID, Produto, Preco FROM dbo.vw_ProdutosAtivos WHERE ProdutoID = 1;
GO
-- teste 2: produto inexistente
DECLARE @Linhas INT;
EXEC dbo.sp_AtualizarPrecoProduto @ProdutoID = 999, @NovoPreco = 50.00, @LinhasAfetadas = @Linhas OUTPUT;
GO
-- teste 3: preço inválido
DECLARE @Linhas INT;
EXEC dbo.sp_AtualizarPrecoProduto @ProdutoID = 1, @NovoPreco = -5.00, @LinhasAfetadas = @Linhas OUTPUT;
GO

-- @passo r3_integracao | Etapa 5 – comparação função escalar x view
USE LojaVirtual;
GO
SELECT r.PedidoID, r.ValorCalculado AS ValorDaView,
       (SELECT SUM(dbo.fn_CalcularSubtotal(ip.Quantidade, ip.PrecoUnitario))
        FROM dbo.ItensPedido AS ip WHERE ip.PedidoID = r.PedidoID) AS ValorDaFuncao,
       p.ValorTotal AS ValorGravadoNoPedido
FROM dbo.vw_ResumoPedidos AS r
INNER JOIN dbo.Pedidos AS p ON p.PedidoID = r.PedidoID
ORDER BY r.PedidoID;
GO
