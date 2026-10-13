using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Servicos;

/// <summary>
/// Uma vez por dia: gera as recorrências pendentes e faz uma cópia consistente do banco
/// (VACUUM INTO) na pasta de backups, mantendo os N mais recentes.
/// </summary>
public class RotinaDiaria(IServiceScopeFactory scopes, IConfiguration config, ILogger<RotinaDiaria> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<RegraService>().Sincronizar();
                await Backup(scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                    scope.ServiceProvider.GetRequiredService<IRelogio>().Hoje, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Falha na rotina diária");
            }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }

    private async Task Backup(AppDbContext db, DateOnly hoje, CancellationToken ct)
    {
        var pasta = config["Backup:Pasta"];
        if (string.IsNullOrWhiteSpace(pasta)) return;
        Directory.CreateDirectory(pasta);

        var arquivo = Path.Combine(pasta, $"financas-{hoje:yyyy-MM-dd}.db");
        if (File.Exists(arquivo)) return;

        await db.Database.ExecuteSqlAsync($"VACUUM INTO {arquivo}", ct);
        log.LogInformation("Backup criado em {Arquivo}", arquivo);

        var manter = config.GetValue("Backup:Manter", 30);
        foreach (var antigo in Directory.GetFiles(pasta, "financas-*.db").OrderDescending().Skip(manter))
            File.Delete(antigo);
    }
}
