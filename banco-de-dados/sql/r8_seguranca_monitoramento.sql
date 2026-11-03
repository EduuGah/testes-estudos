/* =====================================================================
   Programação de Banco de Dados – Roteiro 8
   Segurança, Monitoramento e Boas Práticas em SQL Server
   Nenhuma senha, login de servidor ou dado pessoal é usado neste script.
   ===================================================================== */

-- @passo r8_view | Demonstração – interface de leitura do catálogo
USE LojaVirtual;
GO
CREATE OR ALTER VIEW dbo.vw_CatalogoPublico
AS
SELECT ProdutoID, Nome, Preco,
       CASE WHEN Estoque > 0 THEN 'Disponível' ELSE 'Indisponível' END AS Disponibilidade
FROM dbo.Produtos
WHERE Ativo = 1;
GO
SELECT ProdutoID, Nome, Preco, Disponibilidade FROM dbo.vw_CatalogoPublico;
GO

-- @passo r8_papel | Etapa 1 – papel, usuário sem login e permissões mínimas
USE LojaVirtual;
GO
CREATE ROLE rl_CatalogoLeitura;
GO
CREATE USER usr_CatalogoTeste WITHOUT LOGIN;
GO
ALTER ROLE rl_CatalogoLeitura ADD MEMBER usr_CatalogoTeste;
GO
GRANT SELECT ON dbo.vw_CatalogoPublico TO rl_CatalogoLeitura;
DENY  SELECT ON dbo.Produtos           TO rl_CatalogoLeitura;
GO
SELECT pr.name AS Principal, pe.permission_name AS Permissao, pe.state_desc AS Estado,
       OBJECT_NAME(pe.major_id) AS Objeto
FROM sys.database_permissions AS pe
INNER JOIN sys.database_principals AS pr ON pr.principal_id = pe.grantee_principal_id
WHERE pr.name = 'rl_CatalogoLeitura'
ORDER BY Objeto;
GO

-- @passo r8_teste_acesso | Etapa 2 – acesso permitido e acesso negado
USE LojaVirtual;
GO
EXECUTE AS USER = 'usr_CatalogoTeste';
SELECT USER_NAME() AS UsuarioAtual;
SELECT ProdutoID, Nome, Preco, Disponibilidade FROM dbo.vw_CatalogoPublico;   -- permitido
BEGIN TRY
    SELECT ProdutoID, Nome, Preco, Estoque FROM dbo.Produtos;                -- negado
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS NumeroErro, ERROR_MESSAGE() AS MensagemErro;
END CATCH;
REVERT;
SELECT USER_NAME() AS UsuarioAposRevert;
GO

-- @passo r8_busca | Etapa 3 – busca parametrizada (sp_executesql)
USE LojaVirtual;
GO
CREATE OR ALTER PROCEDURE dbo.sp_BuscarProdutoSeguro
    @Termo NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @SQL NVARCHAR(MAX) =
        N'SELECT ProdutoID, Nome, Preco, Disponibilidade
          FROM dbo.vw_CatalogoPublico
          WHERE Nome LIKE @Filtro;';
    -- o termo vai como VALOR do parâmetro @Filtro, nunca concatenado ao comando
    EXEC sys.sp_executesql @SQL, N'@Filtro NVARCHAR(202)', @Filtro = N'%' + @Termo + N'%';
END;
GO
GRANT EXECUTE ON dbo.sp_BuscarProdutoSeguro TO rl_CatalogoLeitura;
GO
EXECUTE AS USER = 'usr_CatalogoTeste';
EXEC dbo.sp_BuscarProdutoSeguro @Termo = N'Livro';
EXEC dbo.sp_BuscarProdutoSeguro @Termo = N'Copo d''água';     -- apóstrofo tratado como texto
REVERT;
GO

-- @passo r8_monitor_carga | Etapa 4 – sessão com uma consulta em andamento [A]
USE LojaVirtual;
GO
SELECT COUNT(*) AS Pedidos FROM dbo.Pedidos;
WAITFOR DELAY '00:00:05';
GO

-- @passo r8_monitor | Etapa 4 – requisições em andamento (dados de identificação omitidos) [M+2]
SELECT r.session_id, DB_NAME(r.database_id) AS Banco,
       r.status, r.command, r.cpu_time, r.total_elapsed_time, r.wait_type
FROM sys.dm_exec_requests AS r
INNER JOIN sys.dm_exec_sessions AS s ON s.session_id = r.session_id
WHERE r.session_id <> @@SPID AND s.is_user_process = 1
ORDER BY r.total_elapsed_time DESC;
GO

-- @passo r8_querystore | Etapa 5 – situação do Query Store (sem alterar a configuração)
USE LojaVirtual;
GO
SELECT actual_state_desc, desired_state_desc, current_storage_size_mb, readonly_reason
FROM sys.database_query_store_options;
SELECT TOP (5)
       LEFT(REPLACE(REPLACE(qt.query_sql_text, CHAR(13), ' '), CHAR(10), ' '), 60) AS Consulta,
       rs.count_executions AS Execucoes,
       CAST(rs.avg_duration / 1000.0 AS DECIMAL(10,2)) AS DuracaoMediaMs,
       CAST(rs.avg_cpu_time / 1000.0 AS DECIMAL(10,2)) AS CpuMediaMs,
       CAST(rs.avg_logical_io_reads AS DECIMAL(12,1)) AS LeiturasLogicas
FROM sys.query_store_runtime_stats AS rs
INNER JOIN sys.query_store_plan       AS p  ON p.plan_id = rs.plan_id
INNER JOIN sys.query_store_query      AS q  ON q.query_id = p.query_id
INNER JOIN sys.query_store_query_text AS qt ON qt.query_text_id = q.query_text_id
ORDER BY rs.avg_duration DESC;
GO
