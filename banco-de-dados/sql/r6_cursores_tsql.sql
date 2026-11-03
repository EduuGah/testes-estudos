/* =====================================================================
   Programação de Banco de Dados – Roteiro 6
   Processamento de Registros com Cursores e Estruturas T-SQL
   ===================================================================== */

-- @passo r6_demo | Demonstração: variáveis, WHILE, IF...ELSE e CASE
USE LojaVirtual;
GO
DECLARE @Contador INT = 1, @Limite INT = 5;
WHILE @Contador <= @Limite
BEGIN
    IF @Contador % 2 = 0
        PRINT CONCAT(@Contador, ' é par.');
    ELSE
        PRINT CONCAT(@Contador, ' é ímpar.');
    SET @Contador += 1;
END;
SELECT ProdutoID, Nome, Estoque,
       CASE WHEN Estoque = 0  THEN 'Sem estoque'
            WHEN Estoque <= 5  THEN 'Estoque crítico'
            WHEN Estoque <= 15 THEN 'Estoque baixo'
            ELSE 'Estoque adequado' END AS SituacaoEstoque
FROM dbo.Produtos ORDER BY Estoque;
GO

-- @passo r6_tabela | Etapa 1 – tabela de alertas
USE LojaVirtual;
GO
CREATE TABLE dbo.AlertasEstoque (
    AlertaID    INT IDENTITY PRIMARY KEY,
    ProdutoID   INT NOT NULL,
    DataAlerta  DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    NivelAlerta VARCHAR(20)  NOT NULL,
    Mensagem    VARCHAR(200) NOT NULL,
    CONSTRAINT FK_AlertasEstoque_Produtos FOREIGN KEY (ProdutoID) REFERENCES dbo.Produtos(ProdutoID)
);
GO

-- @passo r6_cursor | Etapas 2 e 3 – cursor LOCAL FAST_FORWARD com TRY...CATCH
USE LojaVirtual;
GO
SET STATISTICS TIME ON;
BEGIN TRY
    DECLARE @ProdutoID INT, @Nome VARCHAR(100), @Estoque INT;
    DECLARE @Nivel VARCHAR(20), @Mensagem VARCHAR(200), @Lidos INT = 0;

    DECLARE cur_Estoque CURSOR LOCAL FAST_FORWARD FOR
        SELECT ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE Estoque <= 5 ORDER BY ProdutoID;

    OPEN cur_Estoque;
    FETCH NEXT FROM cur_Estoque INTO @ProdutoID, @Nome, @Estoque;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @Estoque = 0 SET @Nivel = 'URGENTE';
        ELSE            SET @Nivel = 'ATENCAO';
        SET @Mensagem = CONCAT('Produto ', @Nome, ': estoque atual = ', @Estoque, '.');
        INSERT INTO dbo.AlertasEstoque (ProdutoID, NivelAlerta, Mensagem) VALUES (@ProdutoID, @Nivel, @Mensagem);
        SET @Lidos += 1;
        FETCH NEXT FROM cur_Estoque INTO @ProdutoID, @Nome, @Estoque;   -- avança: evita laço infinito
    END;
    CLOSE cur_Estoque;
    DEALLOCATE cur_Estoque;
    PRINT CONCAT('Cursor processou ', @Lidos, ' produto(s).');
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'cur_Estoque') >= 0 CLOSE cur_Estoque;
    IF CURSOR_STATUS('local', 'cur_Estoque') > -3 DEALLOCATE cur_Estoque;
    THROW;
END CATCH;
SET STATISTICS TIME OFF;
SELECT CURSOR_STATUS('local', 'cur_Estoque') AS StatusCursorAoFinal;   -- -3 = não existe mais
SELECT AlertaID, ProdutoID, NivelAlerta, Mensagem FROM dbo.AlertasEstoque ORDER BY AlertaID;
GO

-- @passo r6_cursor_erro | Etapa 3 – erro provocado no meio do cursor e liberação dos recursos
USE LojaVirtual;
GO
BEGIN TRY
    DECLARE @ProdutoID INT, @Nome VARCHAR(100), @Estoque INT;
    DECLARE cur_Estoque CURSOR LOCAL FAST_FORWARD FOR
        SELECT ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE Estoque <= 5 ORDER BY ProdutoID;
    OPEN cur_Estoque;
    FETCH NEXT FROM cur_Estoque INTO @ProdutoID, @Nome, @Estoque;
    -- erro de teste: NivelAlerta não aceita NULL
    INSERT INTO dbo.AlertasEstoque (ProdutoID, NivelAlerta, Mensagem) VALUES (@ProdutoID, NULL, 'teste de erro');
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS NumeroErro, ERROR_MESSAGE() AS Mensagem,
           CURSOR_STATUS('local', 'cur_Estoque') AS StatusAntesDeLiberar;   -- 1 = aberto
    IF CURSOR_STATUS('local', 'cur_Estoque') >= 0 CLOSE cur_Estoque;
    IF CURSOR_STATUS('local', 'cur_Estoque') > -3 DEALLOCATE cur_Estoque;
    SELECT CURSOR_STATUS('local', 'cur_Estoque') AS StatusDepoisDeLiberar;  -- -3 = liberado
END CATCH;
GO

-- @passo r6_conjunto | Etapa 4 – mesma regra com uma única instrução
USE LojaVirtual;
GO
SELECT ProdutoID, NivelAlerta, Mensagem INTO #ResultadoCursor FROM dbo.AlertasEstoque;
DELETE FROM dbo.AlertasEstoque;   -- remove apenas os alertas gerados no laboratório
SET STATISTICS TIME ON;
INSERT INTO dbo.AlertasEstoque (ProdutoID, NivelAlerta, Mensagem)
SELECT ProdutoID,
       CASE WHEN Estoque = 0 THEN 'URGENTE' ELSE 'ATENCAO' END,
       CONCAT('Produto ', Nome, ': estoque atual = ', Estoque, '.')
FROM dbo.Produtos
WHERE Estoque <= 5;
SET STATISTICS TIME OFF;
-- equivalência: diferença entre os dois resultados (vazio = iguais)
SELECT 'só no cursor' AS Origem, * FROM (SELECT ProdutoID, NivelAlerta, Mensagem FROM #ResultadoCursor
                                        EXCEPT SELECT ProdutoID, NivelAlerta, Mensagem FROM dbo.AlertasEstoque) x
UNION ALL
SELECT 'só no conjunto', * FROM (SELECT ProdutoID, NivelAlerta, Mensagem FROM dbo.AlertasEstoque
                                EXCEPT SELECT ProdutoID, NivelAlerta, Mensagem FROM #ResultadoCursor) y;
SELECT (SELECT COUNT(*) FROM #ResultadoCursor) AS LinhasCursor, (SELECT COUNT(*) FROM dbo.AlertasEstoque) AS LinhasConjunto;
GO

-- @passo r6_medicao | Etapa 4 – medição repetida (50 execuções de cada abordagem)
USE LojaVirtual;
GO
DECLARE @i INT, @t0 DATETIME2, @msCursor INT, @msConjunto INT;
DECLARE @ProdutoID INT, @Nome VARCHAR(100), @Estoque INT;

SET @i = 0; SET @t0 = SYSDATETIME();
WHILE @i < 50
BEGIN
    DELETE FROM dbo.AlertasEstoque;
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT ProdutoID, Nome, Estoque FROM dbo.Produtos WHERE Estoque <= 5;
    OPEN cur; FETCH NEXT FROM cur INTO @ProdutoID, @Nome, @Estoque;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        INSERT INTO dbo.AlertasEstoque (ProdutoID, NivelAlerta, Mensagem)
        VALUES (@ProdutoID, CASE WHEN @Estoque = 0 THEN 'URGENTE' ELSE 'ATENCAO' END,
                CONCAT('Produto ', @Nome, ': estoque atual = ', @Estoque, '.'));
        FETCH NEXT FROM cur INTO @ProdutoID, @Nome, @Estoque;
    END;
    CLOSE cur; DEALLOCATE cur;
    SET @i += 1;
END;
SET @msCursor = DATEDIFF(MILLISECOND, @t0, SYSDATETIME());

SET @i = 0; SET @t0 = SYSDATETIME();
WHILE @i < 50
BEGIN
    DELETE FROM dbo.AlertasEstoque;
    INSERT INTO dbo.AlertasEstoque (ProdutoID, NivelAlerta, Mensagem)
    SELECT ProdutoID, CASE WHEN Estoque = 0 THEN 'URGENTE' ELSE 'ATENCAO' END,
           CONCAT('Produto ', Nome, ': estoque atual = ', Estoque, '.')
    FROM dbo.Produtos WHERE Estoque <= 5;
    SET @i += 1;
END;
SET @msConjunto = DATEDIFF(MILLISECOND, @t0, SYSDATETIME());

SELECT 'Cursor (linha a linha)' AS Abordagem, @msCursor AS TempoTotalMs, 50 AS Execucoes UNION ALL
SELECT 'Orientada a conjuntos', @msConjunto, 50;
GO

-- @passo r6_desafio | Etapa 5 – desafio: contagem, IF...ELSE com PRINT e risco geral com CASE
USE LojaVirtual;
GO
DECLARE @Urgentes INT, @Atencao INT;
SELECT @Urgentes = SUM(CASE WHEN NivelAlerta = 'URGENTE' THEN 1 ELSE 0 END),
       @Atencao  = SUM(CASE WHEN NivelAlerta = 'ATENCAO' THEN 1 ELSE 0 END)
FROM dbo.AlertasEstoque;

IF @Urgentes > 0
    PRINT CONCAT('Atenção: ', @Urgentes, ' produto(s) sem estoque precisam de reposição imediata.');
ELSE
    PRINT 'Nenhum produto sem estoque.';

SELECT @Urgentes AS Urgentes, @Atencao AS Atencao,
       CASE WHEN @Urgentes >= 2 THEN 'Alto'
            WHEN @Urgentes = 1 OR @Atencao >= 3 THEN 'Médio'
            ELSE 'Baixo' END AS RiscoGeral;
GO
