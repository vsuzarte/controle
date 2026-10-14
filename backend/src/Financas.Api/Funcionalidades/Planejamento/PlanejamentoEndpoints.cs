using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Comum;

namespace Financas.Api.Funcionalidades.Planejamento;

public static class PlanejamentoEndpoints
{
    public static void MapearPlanejamento(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/planejamento/{mes}");
        g.MapGet("/", (string mes, PlanejamentoService s) => s.Obter(Calendario.ParseMes(mes)));
        g.MapPost("/sugerir", async (string mes, BaseSugestao? @base, PlanejamentoService s) =>
            new { criados = await s.Sugerir(Calendario.ParseMes(mes), @base ?? BaseSugestao.MesAnterior) });
        g.MapPut("/categorias/{categoriaId:guid}", async (string mes, Guid categoriaId, ValorOrcamentoDto dto, PlanejamentoService s) =>
            (await s.DefinirValor(Calendario.ParseMes(mes), categoriaId, dto)).Id);
        g.MapPost("/categorias/{categoriaId:guid}/dispensar", async (string mes, Guid categoriaId, MotivoDto dto, PlanejamentoService s) =>
            (await s.Dispensar(Calendario.ParseMes(mes), categoriaId, dto.Motivo)).Id);
        g.MapPost("/categorias/{categoriaId:guid}/restaurar", async (string mes, Guid categoriaId, PlanejamentoService s) =>
            (await s.Restaurar(Calendario.ParseMes(mes), categoriaId)).Id);
    }
}
