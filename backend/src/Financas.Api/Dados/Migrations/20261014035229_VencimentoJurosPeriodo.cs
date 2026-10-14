using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financas.Api.Dados.Migrations
{
    /// <inheritdoc />
    public partial class VencimentoJurosPeriodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiaUtilNumero",
                table: "Regras",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IniciaPeriodo",
                table: "Regras",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vencimento",
                table: "Regras",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Juros",
                table: "Lancamentos",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiaUtilNumero",
                table: "Regras");

            migrationBuilder.DropColumn(
                name: "IniciaPeriodo",
                table: "Regras");

            migrationBuilder.DropColumn(
                name: "Vencimento",
                table: "Regras");

            migrationBuilder.DropColumn(
                name: "Juros",
                table: "Lancamentos");
        }
    }
}
