using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Comum;

namespace Financas.Api.Funcionalidades.Lancamentos;

public static class LancamentosEndpoints
{
    public static void MapearLancamentos(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/lancamentos");

        g.MapGet("/", (LancamentoConsultas c, string? mes, Guid? contaId, Guid? categoriaId,
            TipoLancamento? tipo, StatusLancamento? status, string? busca, DateOnly? desde) =>
            c.Listar(mes, contaId, categoriaId, tipo, status, busca, desde));
        g.MapGet("/pendentes", (LancamentoConsultas c, Guid categoriaId) => c.Pendentes(categoriaId));
        g.MapGet("/{id:guid}", (Guid id, LancamentoConsultas c) => c.Obter(id));

        g.MapPost("/", async (NovoLancamentoDto dto, LancamentoService s) => (await s.Criar(dto)).Select(l => l.Id));
        g.MapPut("/{id:guid}", async (Guid id, EditarLancamentoDto dto, LancamentoService s) => (await s.Editar(id, dto)).Id);
        g.MapPost("/{id:guid}/confirmar", async (Guid id, ConfirmarDto dto, LancamentoService s) => (await s.Confirmar(id, dto)).Id);
        g.MapPost("/{id:guid}/reabrir", async (Guid id, MotivoDto dto, LancamentoService s) => (await s.Reabrir(id, dto.Motivo)).Id);
        g.MapPost("/{id:guid}/cancelar", async (Guid id, MotivoDto dto, LancamentoService s) => (await s.Cancelar(id, dto.Motivo)).Id);
        g.MapPost("/{id:guid}/estornar", async (Guid id, EstornoDto dto, LancamentoService s) => (await s.Estornar(id, dto)).Id);
    }
}
