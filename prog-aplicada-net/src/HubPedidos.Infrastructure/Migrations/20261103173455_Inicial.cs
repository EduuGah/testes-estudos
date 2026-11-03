using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HubPedidos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Cliente = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Regiao = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Prioridade = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TotalCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Versao = table.Column<int>(type: "INTEGER", nullable: false),
                    Metadados = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Quantidade = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecoCentavos = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_PedidoId",
                table: "ItensPedido",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_Cliente_CriadoEmUtc",
                table: "Pedidos",
                columns: new[] { "Cliente", "CriadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_Status_CriadoEmUtc",
                table: "Pedidos",
                columns: new[] { "Status", "CriadoEmUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItensPedido");

            migrationBuilder.DropTable(
                name: "Pedidos");
        }
    }
}
