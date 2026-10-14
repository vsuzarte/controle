namespace Financas.Api.Funcionalidades.Previsao;

public static class PrevisaoEndpoints
{
    public static void MapearPrevisao(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/previsao");
        // Sem parâmetros: a janela automática de hoje. Com de/ate: o intervalo escolhido.
        g.MapGet("/", (PrevisaoService s, DateOnly? data, DateOnly? de, DateOnly? ate) => s.Obter(data, de, ate));
        g.MapPut("/mover", async (MoverDto dto, QuadroPrevisaoService s) => { await s.Mover(dto); return Results.Ok(); });
        g.MapGet("/config", (ConfiguracaoPrevisaoService s) => s.Ver());
        g.MapPut("/config", (ConfigPrevisaoDto dto, ConfiguracaoPrevisaoService s) => s.Salvar(dto));

        var periodos = g.MapGroup("/periodos");
        periodos.MapGet("/", (PeriodoSalvoService s) => s.Listar());
        periodos.MapPost("/", (PeriodoSalvoDto dto, PeriodoSalvoService s) => s.Criar(dto));
        periodos.MapPut("/{id:guid}", (Guid id, PeriodoSalvoDto dto, PeriodoSalvoService s) => s.Editar(id, dto));
        periodos.MapDelete("/{id:guid}", async (Guid id, PeriodoSalvoService s) => { await s.Excluir(id); return Results.Ok(); });
    }
}
