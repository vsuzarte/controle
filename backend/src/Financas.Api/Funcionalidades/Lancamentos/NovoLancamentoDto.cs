using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Lancamentos;

public record NovoLancamentoDto(
    TipoLancamento Tipo,
    string Descricao,
    decimal Valor,
    DateOnly Data,
    Guid ContaId,
    Guid? ContaDestinoId,
    Guid? CategoriaId,
    FormaPagamento? FormaPagamento,
    string? Observacao,
    StatusLancamento? Status,
    int? Parcelas,
    DateOnly? FaturaMes,
    /// <summary>Compra no vale maior que o saldo: o que faltar sai desta conta (vira um segundo lançamento).</summary>
    Guid? ContaComplementoId = null);
