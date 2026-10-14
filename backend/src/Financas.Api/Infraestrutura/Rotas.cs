using Financas.Api.Dados;
using Financas.Api.Funcionalidades.Auditoria;
using Financas.Api.Funcionalidades.Autenticacao;
using Financas.Api.Funcionalidades.Cartoes;
using Financas.Api.Funcionalidades.Categorias;
using Financas.Api.Funcionalidades.Compras;
using Financas.Api.Funcionalidades.Contas;
using Financas.Api.Funcionalidades.Lancamentos;
using Financas.Api.Funcionalidades.Planejamento;
using Financas.Api.Funcionalidades.Previsao;
using Financas.Api.Funcionalidades.Regras;
using Financas.Api.Funcionalidades.Relatorios;
using Financas.Api.Funcionalidades.Resumo;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Infraestrutura;

/// <summary>Todas as rotas. Cada funcionalidade mapeia as suas; aqui só se junta tudo sob <c>/api</c>.</summary>
public static class Rotas
{
    public static void MapearFinancas(this WebApplication app)
    {
        var senha = app.Configuration["Senha"];

        app.MapGet("/health", async (AppDbContext db) =>
            await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));

        app.MapearAutenticacao(senha);

        // Só no ambiente E2E: recria o banco do zero para cada teste começar limpo.
        if (app.Configuration.GetValue<bool>("E2E"))
        {
            app.MapPost("/api/e2e/reset", async (AppDbContext db) =>
            {
                await db.Database.EnsureDeletedAsync();
                await db.Database.MigrateAsync();
                await DadosIniciais.Aplicar(db);
                return Results.Ok();
            });
        }

        var api = app.MapGroup("/api");
        if (!string.IsNullOrWhiteSpace(senha)) api.RequireAuthorization();

        api.MapearContas();
        api.MapearCategorias();
        api.MapearLancamentos();
        api.MapearRegras();
        api.MapearCartoes();
        api.MapearPlanejamento();
        api.MapearResumo();
        api.MapearPrevisao();
        api.MapearRelatorios();
        api.MapearAuditoria();
        api.MapearCompras();
    }
}
