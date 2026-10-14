using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Infraestrutura;

public static class Inicializacao
{
    /// <summary>Backup (se houver atualização), migrations, modo WAL e cadastros iniciais do primeiro uso.</summary>
    public static async Task PrepararBanco(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await scope.ServiceProvider.GetRequiredService<Backup>().AntesDeAtualizar();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await DadosIniciais.Aplicar(db);
    }

    /// <summary>Erros de regra de negócio viram 400 com mensagem legível; o resto vira 500 genérico (detalhe só no log).</summary>
    public static void UsarTratamentoDeErros(this WebApplication app) =>
        app.UseExceptionHandler(e => e.Run(async ctx =>
        {
            var erro = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
            var (status, mensagem) = erro switch
            {
                RegraNegocioException r => (400, r.Message),
                BadHttpRequestException b => (400, "Requisição inválida: " + b.Message),
                DbUpdateException { InnerException: { } inner } when inner.Message.Contains("UNIQUE") => (409, "Registro duplicado."),
                _ => (500, "Erro inesperado. Veja os logs do servidor."),
            };
            if (status == 500) app.Logger.LogError(erro, "Erro não tratado");
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new { erro = mensagem });
        }));
}
