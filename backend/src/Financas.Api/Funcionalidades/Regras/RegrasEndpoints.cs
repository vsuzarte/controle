namespace Financas.Api.Funcionalidades.Regras;

public static class RegrasEndpoints
{
    public static void MapearRegras(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/regras");
        g.MapGet("/", (RegraConsultas c) => c.Listar());
        g.MapPost("/", async (RegraDto dto, RegraService s) => (await s.Criar(dto)).Id);
        g.MapPut("/{id:guid}", async (Guid id, EditarRegraDto dto, RegraService s) => (await s.Editar(id, dto)).Id);
        g.MapPost("/{id:guid}/desativar", async (Guid id, DesativarRegraDto dto, RegraService s) => (await s.Desativar(id, dto)).Id);
        g.MapPost("/{id:guid}/reativar", async (Guid id, RegraService s) => (await s.Reativar(id)).Id);
    }
}
