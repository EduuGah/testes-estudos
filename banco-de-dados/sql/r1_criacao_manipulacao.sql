/* =====================================================================
   Programação de Banco de Dados – Roteiro 1
   Criação e Manipulação do Banco de Dados de uma Loja Virtual
   Aluno: Carlos Eduardo da Silva de Oliveira – RA 2525997
   ===================================================================== */

-- @passo r1_banco | Etapa 1 – criação do banco
IF DB_ID(N'LojaVirtual') IS NULL
BEGIN
    CREATE DATABASE LojaVirtual;
END;
GO
USE LojaVirtual;
GO
SELECT name AS Banco, create_date AS CriadoEm, collation_name AS Collation
FROM sys.databases WHERE name = N'LojaVirtual';
GO

-- @passo r1_tabelas | Etapa 1 – tabelas na ordem das dependências
USE LojaVirtual;
GO
-- Tabelas independentes primeiro
CREATE TABLE dbo.Clientes (
    ClienteID     INT IDENTITY(1,1) CONSTRAINT PK_Clientes PRIMARY KEY,
    Nome          VARCHAR(100) NOT NULL,
    Email         VARCHAR(150) NOT NULL CONSTRAINT UQ_Clientes_Email UNIQUE,
    Cidade        VARCHAR(80)  NOT NULL,
    Estado        CHAR(2)      NOT NULL,
    DataCadastro  DATE         NOT NULL CONSTRAINT DF_Clientes_DataCadastro DEFAULT (CAST(SYSDATETIME() AS DATE))
);

CREATE TABLE dbo.Categorias (
    CategoriaID   INT IDENTITY(1,1) CONSTRAINT PK_Categorias PRIMARY KEY,
    Nome          VARCHAR(60) NOT NULL CONSTRAINT UQ_Categorias_Nome UNIQUE
);

-- Tabelas dependentes depois
CREATE TABLE dbo.Produtos (
    ProdutoID     INT IDENTITY(1,1) CONSTRAINT PK_Produtos PRIMARY KEY,
    CategoriaID   INT NOT NULL CONSTRAINT FK_Produtos_Categorias REFERENCES dbo.Categorias(CategoriaID),
    Nome          VARCHAR(100)  NOT NULL,
    Preco         DECIMAL(10,2) NOT NULL CONSTRAINT CK_Produtos_Preco CHECK (Preco >= 0),
    Estoque       INT           NOT NULL CONSTRAINT CK_Produtos_Estoque CHECK (Estoque >= 0),
    Ativo         BIT           NOT NULL CONSTRAINT DF_Produtos_Ativo DEFAULT (1)
);

CREATE TABLE dbo.Pedidos (
    PedidoID      INT IDENTITY(1,1) CONSTRAINT PK_Pedidos PRIMARY KEY,
    ClienteID     INT NOT NULL CONSTRAINT FK_Pedidos_Clientes REFERENCES dbo.Clientes(ClienteID),
    DataPedido    DATETIME2(0)  NOT NULL CONSTRAINT DF_Pedidos_Data DEFAULT (SYSDATETIME()),
    StatusPedido  VARCHAR(20)   NOT NULL CONSTRAINT DF_Pedidos_Status DEFAULT ('Pendente')
                  CONSTRAINT CK_Pedidos_Status CHECK (StatusPedido IN ('Pendente','Pago','Enviado','Entregue','Cancelado')),
    ValorTotal    DECIMAL(10,2) NOT NULL CONSTRAINT CK_Pedidos_Valor CHECK (ValorTotal >= 0)
);

CREATE TABLE dbo.ItensPedido (
    ItemPedidoID  INT IDENTITY(1,1) CONSTRAINT PK_ItensPedido PRIMARY KEY,
    PedidoID      INT NOT NULL CONSTRAINT FK_Itens_Pedidos  REFERENCES dbo.Pedidos(PedidoID),
    ProdutoID     INT NOT NULL CONSTRAINT FK_Itens_Produtos REFERENCES dbo.Produtos(ProdutoID),
    Quantidade    INT           NOT NULL CONSTRAINT CK_Itens_Quantidade CHECK (Quantidade > 0),
    PrecoUnitario DECIMAL(10,2) NOT NULL CONSTRAINT CK_Itens_Preco CHECK (PrecoUnitario >= 0)  -- preço praticado na venda
);
GO
SELECT t.name AS Tabela,
       (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id = t.object_id) AS Colunas,
       (SELECT COUNT(*) FROM sys.foreign_keys f WHERE f.parent_object_id = t.object_id) AS ChavesEstrangeiras,
       (SELECT COUNT(*) FROM sys.check_constraints k WHERE k.parent_object_id = t.object_id) AS RestricoesCheck
FROM sys.tables t ORDER BY t.object_id;
GO

-- @passo r1_insercao | Etapa 2 – inserção dos dados
USE LojaVirtual;
GO
INSERT INTO dbo.Categorias (Nome) VALUES ('Eletrônicos'), ('Livros'), ('Casa');

INSERT INTO dbo.Clientes (Nome, Email, Cidade, Estado, DataCadastro) VALUES
('Ana Souza',    'ana.souza@email.com',    'São Paulo',      'SP', '2026-08-02'),
('Bruno Lima',   'bruno.lima@email.com',   'Rio de Janeiro', 'RJ', '2026-08-15'),
('Carla Mendes', 'carla.mendes@email.com', 'Campinas',       'SP', '2026-09-01');

INSERT INTO dbo.Produtos (CategoriaID, Nome, Preco, Estoque) VALUES
(1, 'Fone Bluetooth',               199.90, 25),
(1, 'Teclado Mecânico',             349.90, 12),
(1, 'Mouse sem Fio',                 89.90, 40),
(2, 'Livro SQL Server na Prática',  120.00,  4),
(2, 'Livro Lógica de Programação',   75.50,  0),
(1, 'Monitor 27 polegadas',        1299.00,  8),
(3, 'Luminária LED',                 59.90,  3),
(3, 'Cafeteira Elétrica',           249.00, 15);

INSERT INTO dbo.Pedidos (ClienteID, DataPedido, StatusPedido, ValorTotal) VALUES
(1, '2026-09-10 10:15', 'Entregue',  379.70),
(2, '2026-10-05 14:40', 'Pago',      469.90),
(1, '2026-10-20 09:05', 'Pendente', 1299.00);

INSERT INTO dbo.ItensPedido (PedidoID, ProdutoID, Quantidade, PrecoUnitario) VALUES
(1, 1, 1,  199.90),
(1, 3, 2,   89.90),
(2, 2, 1,  349.90),
(2, 4, 1,  120.00),
(3, 6, 1, 1299.00);
GO
SELECT 'Categorias' AS Tabela, COUNT(*) AS Registros FROM dbo.Categorias UNION ALL
SELECT 'Clientes',  COUNT(*) FROM dbo.Clientes  UNION ALL
SELECT 'Produtos',  COUNT(*) FROM dbo.Produtos  UNION ALL
SELECT 'Pedidos',   COUNT(*) FROM dbo.Pedidos   UNION ALL
SELECT 'ItensPedido', COUNT(*) FROM dbo.ItensPedido;
GO

-- @passo r1_restricao | Teste das restrições (erros esperados)
USE LojaVirtual;
GO
INSERT INTO dbo.Produtos (CategoriaID, Nome, Preco, Estoque) VALUES (1, 'Produto inválido', -10.00, 5);
GO
INSERT INTO dbo.Clientes (Nome, Email, Cidade, Estado) VALUES ('Ana Repetida', 'ana.souza@email.com', 'Santos', 'SP');
GO

-- @passo r1_consultas | Etapa 3 – consultas de conferência
USE LojaVirtual;
GO
-- 1) clientes em ordem alfabética
SELECT ClienteID, Nome, Email, Cidade, Estado FROM dbo.Clientes ORDER BY Nome;
-- 2) produtos ativos com preço inferior a R$ 500,00
SELECT ProdutoID, Nome, Preco FROM dbo.Produtos WHERE Ativo = 1 AND Preco < 500.00 ORDER BY Preco;
-- 3) produto, categoria, preço e estoque (INNER JOIN)
SELECT p.Nome AS Produto, c.Nome AS Categoria, p.Preco, p.Estoque
FROM dbo.Produtos AS p INNER JOIN dbo.Categorias AS c ON c.CategoriaID = p.CategoriaID
ORDER BY c.Nome, p.Nome;
-- 4) pedidos com o nome do cliente
SELECT p.PedidoID, c.Nome AS Cliente, p.DataPedido, p.StatusPedido, p.ValorTotal
FROM dbo.Pedidos AS p INNER JOIN dbo.Clientes AS c ON c.ClienteID = p.ClienteID
ORDER BY p.PedidoID;
-- 5) subtotal de cada item
SELECT p.PedidoID, c.Nome AS Cliente, pr.Nome AS Produto, ip.Quantidade, ip.PrecoUnitario,
       ip.Quantidade * ip.PrecoUnitario AS Subtotal
FROM dbo.Pedidos AS p
INNER JOIN dbo.Clientes    AS c  ON c.ClienteID = p.ClienteID
INNER JOIN dbo.ItensPedido AS ip ON ip.PedidoID = p.PedidoID
INNER JOIN dbo.Produtos    AS pr ON pr.ProdutoID = ip.ProdutoID
ORDER BY p.PedidoID, pr.Nome;
-- 6) valor dos pedidos por cliente (GROUP BY)
SELECT c.Nome AS Cliente, COUNT(*) AS Pedidos, SUM(p.ValorTotal) AS ValorTotal
FROM dbo.Pedidos AS p INNER JOIN dbo.Clientes AS c ON c.ClienteID = p.ClienteID
GROUP BY c.Nome ORDER BY ValorTotal DESC;
GO

-- @passo r1_atualizacao | Etapa 4 – atualizações conferidas antes e depois
USE LojaVirtual;
GO
SELECT ProdutoID, Nome, Preco, Estoque FROM dbo.Produtos WHERE ProdutoID = 3;   -- antes
UPDATE dbo.Produtos SET Preco = 99.90, Estoque = Estoque + 10 WHERE ProdutoID = 3;
SELECT ProdutoID, Nome, Preco, Estoque FROM dbo.Produtos WHERE ProdutoID = 3;   -- depois

UPDATE dbo.Clientes SET Cidade = 'Niterói' WHERE ClienteID = 2;
SELECT ClienteID, Nome, Cidade, Estado FROM dbo.Clientes WHERE ClienteID = 2;

-- exclusão lógica: o produto sai do catálogo, mas o histórico continua
UPDATE dbo.Produtos SET Ativo = 0 WHERE ProdutoID = 8;
SELECT ProdutoID, Nome, Ativo FROM dbo.Produtos WHERE ProdutoID = 8;
GO

-- @passo r1_exclusao | Etapa 5 – exclusão controlada
USE LojaVirtual;
GO
INSERT INTO dbo.Clientes (Nome, Email, Cidade, Estado) VALUES ('Cliente Teste', 'cliente.teste@email.com', 'Santos', 'SP');
SELECT ClienteID, Nome, Email FROM dbo.Clientes WHERE Email = 'cliente.teste@email.com';
DELETE FROM dbo.Clientes WHERE Email = 'cliente.teste@email.com';
SELECT @@ROWCOUNT AS QuantidadeExcluida;
GO
-- tentativa de excluir cliente com pedidos: a chave estrangeira impede
DELETE FROM dbo.Clientes WHERE ClienteID = 1;
GO
