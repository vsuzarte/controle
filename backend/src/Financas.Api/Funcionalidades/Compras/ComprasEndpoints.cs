namespace Financas.Api.Funcionalidades.Compras;

public static class ComprasEndpoints
{
    public static void MapearCompras(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/compras");
        g.MapPost("/ler", (LerNotaDto dto, CompraService s, CancellationToken ct) => s.Ler(dto.Url, ct));
        g.MapPost("/", async (ImportarCompraDto dto, CompraService s, CancellationToken ct) => (await s.Importar(dto, ct)).Id);
        g.MapGet("/{id:guid}", (Guid id, ComprasConsultas c) => c.Obter(id));
    }
}
