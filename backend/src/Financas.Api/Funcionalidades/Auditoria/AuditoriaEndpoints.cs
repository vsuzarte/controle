namespace Financas.Api.Funcionalidades.Auditoria;

public static class AuditoriaEndpoints
{
    public static void MapearAuditoria(this RouteGroupBuilder api) =>
        api.MapGet("/auditoria", (AuditoriaService s, Guid? entidadeId, int? limite) => s.Listar(entidadeId, limite));
}
