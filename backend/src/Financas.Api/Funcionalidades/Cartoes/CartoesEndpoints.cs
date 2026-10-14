using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Regras;

namespace Financas.Api.Funcionalidades.Cartoes;

public static class CartoesEndpoints
{
    public static void MapearCartoes(this RouteGroupBuilder api)
    {
        api.MapGet("/cartoes/todos", async (CartaoService s, RegraService regras) =>
        {
            await regras.Sincronizar();
            return await s.Todos();
        });

        var g = api.MapGroup("/cartoes/{id:guid}/faturas");
        g.MapGet("/", async (Guid id, CartaoService s, RegraService regras) =>
        {
            await regras.Sincronizar();
            return (await s.Faturas(id)).Select(f => f with { Lancamentos = [], Pagamentos = [] });
        });
        g.MapGet("/{mes}", async (Guid id, string mes, CartaoService s, RegraService regras) =>
        {
            var m = Calendario.ParseMes(mes);
            await regras.Sincronizar(Calendario.FimMes(m.AddMonths(1)));
            return await s.Fatura(id, m);
        });
        g.MapPost("/{mes}/pagar", async (Guid id, string mes, PagarFaturaDto dto, CartaoService s) =>
            (await s.Pagar(id, Calendario.ParseMes(mes), dto)).Id);
    }
}
