CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "Pedidos" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Pedidos" PRIMARY KEY,
    "Cliente" TEXT NOT NULL,
    "Regiao" TEXT NOT NULL,
    "Prioridade" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "TotalCentavos" INTEGER NOT NULL,
    "CriadoEmUtc" TEXT NOT NULL,
    "AtualizadoEmUtc" TEXT NOT NULL,
    "Versao" INTEGER NOT NULL,
    "Metadados" TEXT NOT NULL
);

CREATE TABLE "ItensPedido" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ItensPedido" PRIMARY KEY AUTOINCREMENT,
    "PedidoId" TEXT NOT NULL,
    "Sku" TEXT NOT NULL,
    "Quantidade" INTEGER NOT NULL,
    "PrecoCentavos" INTEGER NOT NULL,
    CONSTRAINT "FK_ItensPedido_Pedidos_PedidoId" FOREIGN KEY ("PedidoId") REFERENCES "Pedidos" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_ItensPedido_PedidoId" ON "ItensPedido" ("PedidoId");

CREATE INDEX "IX_Pedidos_Cliente_CriadoEmUtc" ON "Pedidos" ("Cliente", "CriadoEmUtc");

CREATE INDEX "IX_Pedidos_Status_CriadoEmUtc" ON "Pedidos" ("Status", "CriadoEmUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261103173455_Inicial', '10.0.14');

COMMIT;

