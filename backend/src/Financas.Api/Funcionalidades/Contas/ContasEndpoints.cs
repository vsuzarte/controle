namespace Financas.Api.Funcionalidades.Contas;

public static class ContasEndpoints
{
    public static void MapearContas(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/contas");
        g.MapGet("/", (ContaService s) => s.Listar());
        g.MapPost("/", (ContaDto dto, ContaService s) => s.Criar(dto));
        g.MapPut("/{id:guid}", (Guid id, ContaDto dto, ContaService s) => s.Editar(id, dto));
        g.MapPut("/{id:guid}/limite", async (Guid id, LimiteDto dto, ContaService s) =>
        {
            var c = await s.AjustarLimite(id, dto);
            return new { c.Id, c.Limite };
        });
    }
}
