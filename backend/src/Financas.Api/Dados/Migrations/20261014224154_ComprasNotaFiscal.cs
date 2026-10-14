using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financas.Api.Dados.Migrations
{
    /// <inheritdoc />
    public partial class ComprasNotaFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompraId",
                table: "Lancamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Compras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChaveAcesso = table.Column<string>(type: "TEXT", maxLength: 44, nullable: false),
                    Loja = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Cnpj = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Emissao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Total = table.Column<long>(type: "INTEGER", nullable: false),
                    Desconto = table.Column<long>(type: "INTEGER", nullable: false),
                    FormaPagamentoNota = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ContaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Compras_Contas_ContaId",
                        column: x => x.ContaId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItensCompra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequencia = table.Column<int>(type: "INTEGER", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Quantidade = table.Column<long>(type: "INTEGER", nullable: false),
                    Unidade = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ValorUnitario = table.Column<long>(type: "INTEGER", nullable: false),
                    ValorTotal = table.Column<long>(type: "INTEGER", nullable: false),
                    ValorPago = table.Column<long>(type: "INTEGER", nullable: false),
                    CategoriaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LancamentoId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensCompra_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensCompra_Compras_CompraId",
                        column: x => x.CompraId,
                        principalTable: "Compras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensCompra_Lancamentos_LancamentoId",
                        column: x => x.LancamentoId,
                        principalTable: "Lancamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_CompraId",
                table: "Lancamentos",
                column: "CompraId");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_ChaveAcesso",
                table: "Compras",
                column: "ChaveAcesso",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Compras_ContaId",
                table: "Compras",
                column: "ContaId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensCompra_CategoriaId",
                table: "ItensCompra",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensCompra_CompraId_Sequencia",
                table: "ItensCompra",
                columns: new[] { "CompraId", "Sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensCompra_Descricao",
                table: "ItensCompra",
                column: "Descricao");

            migrationBuilder.CreateIndex(
                name: "IX_ItensCompra_LancamentoId",
                table: "ItensCompra",
                column: "LancamentoId");

            // Como nas outras tabelas: compras e itens nunca são apagados.
            foreach (var tabela in new[] { "Compras", "ItensCompra" })
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER trg_{tabela}_sem_delete BEFORE DELETE ON {tabela}
                    BEGIN SELECT RAISE(ABORT, 'Exclusão não permitida em {tabela}'); END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItensCompra");

            migrationBuilder.DropTable(
                name: "Compras");

            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_CompraId",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "CompraId",
                table: "Lancamentos");
        }
    }
}
