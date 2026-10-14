namespace Financas.Api.Funcionalidades.Resumo;

public static class ResumoEndpoints
{
    public static void MapearResumo(this RouteGroupBuilder api) =>
        api.MapGet("/resumo", (ResumoService s) => s.Obter());
}
