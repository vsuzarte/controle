using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Lancamentos;

public record EditarLancamentoDto(
    string Descricao,
    decimal Valor,
    DateOnly Data,
    Guid ContaId,
    Guid? ContaDestinoId,
    Guid? CategoriaId,
    FormaPagamento? FormaPagamento,
    string? Observacao,
    DateOnly? FaturaMes,
    string? Motivo);
