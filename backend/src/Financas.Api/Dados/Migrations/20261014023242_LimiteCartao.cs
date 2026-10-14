using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financas.Api.Dados.Migrations
{
    /// <inheritdoc />
    public partial class LimiteCartao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Limite",
                table: "Contas",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Limite",
                table: "Contas");
        }
    }
}
