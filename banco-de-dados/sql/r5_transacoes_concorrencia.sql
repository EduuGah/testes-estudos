/* =====================================================================
   Programação de Banco de Dados – Roteiro 5
   Controle de Transações e Concorrência em Pedidos e Estoques
   Os blocos marcados [A] e [B] foram executados em duas sessões
   (janelas) diferentes, ao mesmo tempo; [B+2] = iniciada 2 s depois.
   ===================================================================== */

-- @passo r5_demo | Demonstração: alteração temporária desfeita com ROLLBACK
USE LojaVirtual;
GO
SELECT 'antes' AS Momento, ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE ProdutoID = 1;
BEGIN TRANSACTION;
UPDATE dbo.Produtos SET Estoque = Estoque - 1 WHERE ProdutoID = 1 AND Estoque >= 1;
SELECT 'dentro da transação' AS Momento, ProdutoID, Nome, Estoque, @@TRANCOUNT AS TranCount FROM dbo.Produtos WHERE ProdutoID = 1;
ROLLBACK TRANSACTION;
SELECT 'após ROLLBACK' AS Momento, ProdutoID, Nome, Estoque, @@TRANCOUNT AS TranCount FROM dbo.Produtos WHERE ProdutoID = 1;
GO
BEGIN TRANSACTION;
UPDATE dbo.Produtos SET Estoque = Estoque + 2 WHERE ProdutoID = 1;
COMMIT TRANSACTION;
SELECT 'após COMMIT (+2)' AS Momento, ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE ProdutoID = 1;
GO

-- @passo r5_pagamentos | Etapa 1 – tabela de pagamentos
USE LojaVirtual;
GO
CREATE TABLE dbo.Pagamentos (
    PagamentoID    INT IDENTITY PRIMARY KEY,
    PedidoID       INT NOT NULL UNIQUE,
    DataPagamento  DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ValorPago      DECIMAL(10,2) NOT NULL CHECK (ValorPago > 0),
    FormaPagamento VARCHAR(30) NOT NULL,
    CONSTRAINT FK_Pagamentos_Pedidos FOREIGN KEY (PedidoID) REFERENCES dbo.Pedidos(PedidoID)
);
GO

-- @passo r5_procedure | Etapa 2 – procedure transacional de pagamento
USE LojaVirtual;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ProcessarPagamento
    @PedidoID       INT,
    @FormaPagamento VARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;            -- qualquer erro torna a transação não confirmável
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status VARCHAR(20), @Valor DECIMAL(10,2);
        -- UPDLOCK + HOLDLOCK: bloqueia o pedido até o fim da transação
        SELECT @Status = StatusPedido, @Valor = ValorTotal
        FROM dbo.Pedidos WITH (UPDLOCK, HOLDLOCK)
        WHERE PedidoID = @PedidoID;

        IF @Status IS NULL      THROW 50010, 'Pedido não encontrado.', 1;
        IF @Status <> 'Pendente' THROW 50011, 'Pedido não está pendente.', 1;

        INSERT INTO dbo.Pagamentos (PedidoID, ValorPago, FormaPagamento)
        VALUES (@PedidoID, @Valor, @FormaPagamento);

        -- aciona trg_ValidarEstoquePedido (Roteiro 4) dentro da mesma transação
        UPDATE dbo.Pedidos SET StatusPedido = 'Pago' WHERE PedidoID = @PedidoID;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- @passo r5_teste_valido | Teste: pagamento válido (pedido 3)
USE LojaVirtual;
GO
SELECT 'antes' AS Momento, p.PedidoID, p.StatusPedido, pr.Nome AS Produto, pr.Estoque
FROM dbo.Pedidos p JOIN dbo.ItensPedido ip ON ip.PedidoID = p.PedidoID JOIN dbo.Produtos pr ON pr.ProdutoID = ip.ProdutoID
WHERE p.PedidoID = 3;
EXEC dbo.sp_ProcessarPagamento @PedidoID = 3, @FormaPagamento = 'Pix';
SELECT 'depois' AS Momento, p.PedidoID, p.StatusPedido, pr.Nome AS Produto, pr.Estoque
FROM dbo.Pedidos p JOIN dbo.ItensPedido ip ON ip.PedidoID = p.PedidoID JOIN dbo.Produtos pr ON pr.ProdutoID = ip.ProdutoID
WHERE p.PedidoID = 3;
SELECT PagamentoID, PedidoID, ValorPago, FormaPagamento FROM dbo.Pagamentos;
GO

-- @passo r5_teste_erros | Testes de erro: inexistente, já pago e sem estoque
USE LojaVirtual;
GO
EXEC dbo.sp_ProcessarPagamento @PedidoID = 999, @FormaPagamento = 'Pix';
GO
EXEC dbo.sp_ProcessarPagamento @PedidoID = 6, @FormaPagamento = 'Cartão';
GO
EXEC dbo.sp_ProcessarPagamento @PedidoID = 7, @FormaPagamento = 'Boleto';
GO
-- nenhum pagamento parcial ficou gravado e nenhuma transação ficou aberta
SELECT p.PedidoID, p.StatusPedido,
       (SELECT COUNT(*) FROM dbo.Pagamentos pg WHERE pg.PedidoID = p.PedidoID) AS Pagamentos
FROM dbo.Pedidos p WHERE p.PedidoID IN (6, 7);
SELECT @@TRANCOUNT AS TransacoesAbertas;
GO

-- @passo r5_bloqueio_A | Etapa 3 – Sessão A segura o bloqueio por 8 s [A]
USE LojaVirtual;
GO
SELECT CONVERT(VARCHAR(12), SYSDATETIME(), 114) AS Hora, 'Sessão A: BEGIN TRAN + UPDATE' AS Evento, @@SPID AS Sessao;
BEGIN TRANSACTION;
UPDATE dbo.Produtos SET Estoque = Estoque + 1 WHERE ProdutoID = 1;
WAITFOR DELAY '00:00:08';          -- transação aberta, sem COMMIT
ROLLBACK TRANSACTION;
SELECT CONVERT(VARCHAR(12), SYSDATETIME(), 114) AS Hora, 'Sessão A: ROLLBACK' AS Evento, @@TRANCOUNT AS TranCount;
GO

-- @passo r5_bloqueio_B | Etapa 3 – Sessão B tenta alterar o mesmo produto [B+2]
USE LojaVirtual;
GO
DECLARE @Inicio DATETIME2 = SYSDATETIME();
UPDATE dbo.Produtos SET Estoque = Estoque + 1 WHERE ProdutoID = 1;
SELECT CONVERT(VARCHAR(12), @Inicio, 114) AS Inicio, CONVERT(VARCHAR(12), SYSDATETIME(), 114) AS Fim,
       DATEDIFF(MILLISECOND, @Inicio, SYSDATETIME()) AS EsperaMs, @@SPID AS Sessao;
UPDATE dbo.Produtos SET Estoque = Estoque - 1 WHERE ProdutoID = 1;   -- desfaz o +1 do teste
SELECT @@TRANCOUNT AS TranCount;
GO

-- @passo r5_bloqueio_M | Etapa 3 – observação do bloqueio em andamento [M+4]
SELECT r.session_id AS SessaoBloqueada, r.blocking_session_id AS BloqueadaPor,
       r.command, r.wait_type, r.wait_time AS EsperaMs
FROM sys.dm_exec_requests AS r
WHERE r.blocking_session_id <> 0;
GO

-- @passo r5_preparar_concorrencia | Etapa 4 – novo pedido pendente
USE LojaVirtual;
GO
INSERT INTO dbo.Pedidos (ClienteID, DataPedido, StatusPedido, ValorTotal) VALUES (3, '2026-10-27 15:00', 'Pendente', 99.90);
INSERT INTO dbo.ItensPedido (PedidoID, ProdutoID, Quantidade, PrecoUnitario) VALUES (SCOPE_IDENTITY(), 3, 1, 99.90);
SELECT TOP (1) p.PedidoID, p.StatusPedido, pr.Nome AS Produto, pr.Estoque
FROM dbo.Pedidos p JOIN dbo.ItensPedido ip ON ip.PedidoID = p.PedidoID JOIN dbo.Produtos pr ON pr.ProdutoID = ip.ProdutoID
ORDER BY p.PedidoID DESC;
GO

-- @passo r5_pagto_A | Etapa 4 – Sessão A paga o pedido 10 e demora para confirmar [A]
USE LojaVirtual;
GO
BEGIN TRANSACTION;
EXEC dbo.sp_ProcessarPagamento @PedidoID = 10, @FormaPagamento = 'Pix';
SELECT CONVERT(VARCHAR(12), SYSDATETIME(), 114) AS Hora, 'Sessão A: pagamento feito, aguardando COMMIT' AS Evento;
WAITFOR DELAY '00:00:06';
COMMIT TRANSACTION;
SELECT CONVERT(VARCHAR(12), SYSDATETIME(), 114) AS Hora, 'Sessão A: COMMIT' AS Evento, @@TRANCOUNT AS TranCount;
GO

-- @passo r5_pagto_B | Etapa 4 – Sessão B tenta pagar o mesmo pedido [B+2]
USE LojaVirtual;
GO
SELECT CONVERT(VARCHAR(12), SYSDATETIME(), 114) AS Hora, 'Sessão B: chamando a procedure' AS Evento;
EXEC dbo.sp_ProcessarPagamento @PedidoID = 10, @FormaPagamento = 'Cartão';
GO
SELECT CONVERT(VARCHAR(12), SYSDATETIME(), 114) AS Hora, 'Sessão B: liberada' AS Evento, @@TRANCOUNT AS TranCount;
GO

-- @passo r5_pagto_conferencia | Etapa 4 – conferência: um único pagamento e uma única baixa
USE LojaVirtual;
GO
SELECT p.PedidoID, p.StatusPedido,
       (SELECT COUNT(*) FROM dbo.Pagamentos pg WHERE pg.PedidoID = p.PedidoID) AS Pagamentos,
       (SELECT Estoque FROM dbo.Produtos WHERE ProdutoID = 3) AS EstoqueMouse
FROM dbo.Pedidos p WHERE p.PedidoID = 10;
GO

-- @passo r5_isolamento | Etapa 5 – nível de isolamento padrão
DBCC USEROPTIONS WITH NO_INFOMSGS;
GO

-- @passo r5_rc_A | READ COMMITTED: Sessão A mantém uma alteração aberta [A]
USE LojaVirtual;
GO
BEGIN TRANSACTION;
UPDATE dbo.Produtos SET Estoque = Estoque + 1 WHERE ProdutoID = 1;
WAITFOR DELAY '00:00:05';
ROLLBACK TRANSACTION;
SELECT @@TRANCOUNT AS TranCount;
GO

-- @passo r5_rc_B | READ COMMITTED: Sessão B tenta ler a linha alterada [B+2]
USE LojaVirtual;
GO
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SET LOCK_TIMEOUT 2000;     -- desiste depois de 2 s em vez de esperar indefinidamente
SELECT ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE ProdutoID = 1;
GO

-- @passo r5_ser_A | SERIALIZABLE: Sessão A lê os pedidos de um cliente e mantém a transação [A]
USE LojaVirtual;
GO
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
SELECT COUNT(*) AS PedidosDaElisa FROM dbo.Pedidos WHERE ClienteID = 6;
WAITFOR DELAY '00:00:05';
SELECT COUNT(*) AS PedidosDaElisaNovamente FROM dbo.Pedidos WHERE ClienteID = 6;
COMMIT TRANSACTION;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
GO

-- @passo r5_ser_B | SERIALIZABLE: Sessão B tenta inserir um pedido no mesmo intervalo [B+2]
USE LojaVirtual;
GO
SET LOCK_TIMEOUT 2000;
INSERT INTO dbo.Pedidos (ClienteID, StatusPedido, ValorTotal) VALUES (6, 'Cancelado', 0);
GO

-- @passo r5_final | Confirmação: nenhuma transação aberta
SELECT COUNT(*) AS TransacoesDeUsuarioAbertas
FROM sys.dm_tran_session_transactions AS t
INNER JOIN sys.dm_exec_sessions AS s ON s.session_id = t.session_id
WHERE s.is_user_process = 1 AND t.session_id <> @@SPID;
SELECT @@TRANCOUNT AS TranCountDestaSessao;
GO
