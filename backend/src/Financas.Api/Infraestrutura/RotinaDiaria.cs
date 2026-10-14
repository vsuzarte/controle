using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Regras;

namespace Financas.Api.Infraestrutura;

/// <summary>
/// De hora em hora: gera as recorrências pendentes e, uma vez por dia, faz a cópia do banco
/// na pasta de backups.
/// </summary>
public class RotinaDiaria(IServiceScopeFactory scopes, ILogger<RotinaDiaria> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var servicos = scope.ServiceProvider;
                await servicos.GetRequiredService<RegraService>().Sincronizar();
                await servicos.GetRequiredService<Backup>().Diario(servicos.GetRequiredService<IRelogio>().Hoje, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Falha na rotina diária");
            }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
