using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financas.Api.Dados.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Entidade = table.Column<string>(type: "TEXT", nullable: false),
                    EntidadeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Operacao = table.Column<string>(type: "TEXT", nullable: false),
                    Antes = table.Column<string>(type: "TEXT", nullable: true),
                    Depois = table.Column<string>(type: "TEXT", nullable: true),
                    Motivo = table.Column<string>(type: "TEXT", nullable: true),
                    DataHora = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Grupo = table.Column<string>(type: "TEXT", nullable: true),
                    Cor = table.Column<string>(type: "TEXT", nullable: true),
                    Icone = table.Column<string>(type: "TEXT", nullable: true),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    SaldoInicial = table.Column<long>(type: "INTEGER", nullable: false),
                    Cor = table.Column<string>(type: "TEXT", nullable: true),
                    DiaFechamento = table.Column<int>(type: "INTEGER", nullable: true),
                    DiaVencimento = table.Column<int>(type: "INTEGER", nullable: true),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrcamentoItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mes = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CategoriaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Valor = table.Column<long>(type: "INTEGER", nullable: false),
                    ValorSugerido = table.Column<long>(type: "INTEGER", nullable: false),
                    Editado = table.Column<bool>(type: "INTEGER", nullable: false),
                    Dispensado = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrcamentoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrcamentoItens_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Regras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Modo = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CategoriaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FormaPagamento = table.Column<string>(type: "TEXT", nullable: true),
                    Valor = table.Column<long>(type: "INTEGER", nullable: false),
                    ValorVariavel = table.Column<bool>(type: "INTEGER", nullable: false),
                    Parcelas = table.Column<int>(type: "INTEGER", nullable: true),
                    Frequencia = table.Column<string>(type: "TEXT", nullable: false),
                    Intervalo = table.Column<int>(type: "INTEGER", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regras_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Regras_Contas_ContaId",
                        column: x => x.ContaId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lancamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Valor = table.Column<long>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ContaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContaDestinoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CategoriaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FormaPagamento = table.Column<string>(type: "TEXT", nullable: true),
                    Observacao = table.Column<string>(type: "TEXT", nullable: true),
                    Programado = table.Column<bool>(type: "INTEGER", nullable: false),
                    RegraId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Sequencia = table.Column<int>(type: "INTEGER", nullable: true),
                    FaturaMes = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EstornoDeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "TEXT", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lancamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Contas_ContaDestinoId",
                        column: x => x.ContaDestinoId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Contas_ContaId",
                        column: x => x.ContaId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Lancamentos_EstornoDeId",
                        column: x => x.EstornoDeId,
                        principalTable: "Lancamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Regras_RegraId",
                        column: x => x.RegraId,
                        principalTable: "Regras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_DataHora",
                table: "AuditLogs",
                column: "DataHora");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entidade_EntidadeId",
                table: "AuditLogs",
                columns: new[] { "Entidade", "EntidadeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Nome_Tipo",
                table: "Categorias",
                columns: new[] { "Nome", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_CategoriaId",
                table: "Lancamentos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_ContaDestinoId",
                table: "Lancamentos",
                column: "ContaDestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_ContaId",
                table: "Lancamentos",
                column: "ContaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_Data",
                table: "Lancamentos",
                column: "Data");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_EstornoDeId",
                table: "Lancamentos",
                column: "EstornoDeId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_FaturaMes",
                table: "Lancamentos",
                column: "FaturaMes");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_RegraId_Sequencia",
                table: "Lancamentos",
                columns: new[] { "RegraId", "Sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrcamentoItens_CategoriaId",
                table: "OrcamentoItens",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_OrcamentoItens_Mes_CategoriaId",
                table: "OrcamentoItens",
                columns: new[] { "Mes", "CategoriaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regras_CategoriaId",
                table: "Regras",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Regras_ContaId",
                table: "Regras",
                column: "ContaId");

            // Reforço no banco: nenhum registro financeiro ou de auditoria pode ser excluído,
            // nem por código com bug nem por alguém mexendo direto no arquivo .db.
            foreach (var tabela in new[] { "Contas", "Categorias", "Lancamentos", "Regras", "OrcamentoItens", "AuditLogs" })
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER trg_{tabela}_sem_delete BEFORE DELETE ON {tabela}
                    BEGIN SELECT RAISE(ABORT, 'Exclusão não permitida em {tabela}'); END;
                    """);
            }
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_AuditLogs_sem_update BEFORE UPDATE ON AuditLogs
                BEGIN SELECT RAISE(ABORT, 'Auditoria não pode ser alterada'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Lancamentos");

            migrationBuilder.DropTable(
                name: "OrcamentoItens");

            migrationBuilder.DropTable(
                name: "Regras");

            migrationBuilder.DropTable(
                name: "Categorias");

            migrationBuilder.DropTable(
                name: "Contas");
        }
    }
}
