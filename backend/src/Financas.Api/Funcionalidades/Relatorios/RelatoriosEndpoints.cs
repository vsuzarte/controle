using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Relatorios;

public static class RelatoriosEndpoints
{
    public static void MapearRelatorios(this RouteGroupBuilder api) =>
        api.MapGet("/relatorios/comparativo", (RelatorioService s, IRelogio relogio, string? mes, int? meses) =>
            s.Comparativo(string.IsNullOrEmpty(mes) ? relogio.Hoje : Calendario.ParseMes(mes), meses ?? 6));
}
