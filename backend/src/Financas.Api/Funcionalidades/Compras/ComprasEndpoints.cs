using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Compras;

public static class ComprasEndpoints
{
    public static void MapearCompras(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/compras");
        g.MapPost("/ler", (LerNotaDto dto, CompraService s, CancellationToken ct) => s.Ler(dto.Url, ct));
        g.MapPost("/", async (ImportarCompraDto dto, CompraService s, CancellationToken ct) => (await s.Importar(dto, ct)).Id);
        g.MapGet("/", (ComprasConsultas c, int? limite) => c.Recentes(limite ?? 30));
        g.MapGet("/produtos", (ComprasConsultas c, IRelogio relogio, string? busca, DateOnly? de, DateOnly? ate) =>
            c.Produtos(busca, de ?? relogio.Hoje.AddYears(-1), ate ?? relogio.Hoje));
        g.MapGet("/{id:guid}", (Guid id, ComprasConsultas c) => c.Obter(id));
    }
}
