using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financas.Api.Dados.Migrations
{
    /// <inheritdoc />
    public partial class QuadroPrevisao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PagamentosPlanejados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LancamentoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CartaoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FaturaMes = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PagarEm = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagamentosPlanejados", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosPlanejados_CartaoId_FaturaMes",
                table: "PagamentosPlanejados",
                columns: new[] { "CartaoId", "FaturaMes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosPlanejados_LancamentoId",
                table: "PagamentosPlanejados",
                column: "LancamentoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PagamentosPlanejados");
        }
    }
}
