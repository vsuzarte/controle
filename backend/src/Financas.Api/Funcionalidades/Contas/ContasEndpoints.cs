using Financas.Api.Funcionalidades.Comum;

namespace Financas.Api.Funcionalidades.Contas;

public static class ContasEndpoints
{
    public static void MapearContas(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/contas");
        g.MapGet("/", (ContaService s) => s.Listar());
        g.MapPost("/", (ContaDto dto, ContaService s) => s.Criar(dto));
        g.MapPut("/{id:guid}", (Guid id, ContaDto dto, ContaService s) => s.Editar(id, dto));
        g.MapPost("/{id:guid}/desativar", async (Guid id, MotivoDto dto, ContaService s) => (await s.Desativar(id, dto.Motivo)).Id);
        g.MapPost("/{id:guid}/reativar", async (Guid id, ContaService s) => (await s.Reativar(id)).Id);
        g.MapPut("/{id:guid}/limite", async (Guid id, LimiteDto dto, ContaService s) =>
        {
            var c = await s.AjustarLimite(id, dto);
            return new { c.Id, c.Limite };
        });
        g.MapPut("/{id:guid}/recarga", (Guid id, RecargaDto dto, RecargaDoVale s) => s.Definir(id, dto));
    }
}
