using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Regras;

public record EditarRegraDto(
    string Descricao,
    Guid CategoriaId,
    Guid ContaId,
    FormaPagamento? FormaPagamento,
    decimal Valor,
    bool ValorVariavel,
    DateOnly? DataFim,
    string? Observacao,
    bool AplicarAosPrevistos,
    string? Motivo,
    /// <summary>Mensal: nova forma de vencimento. Nulo = não muda. Ao mudar, os previstos a partir deste mês são recalculados.</summary>
    RegraVencimento? Vencimento = null,
    int? DiaUtilNumero = null,
    /// <summary>Com Vencimento = DiaFixo: novo dia do mês.</summary>
    int? Dia = null,
    bool? IniciaPeriodo = null,
    /// <summary>Contas: dia de pagamento que paga esta conta (nulo = automático). Sempre aplicado na edição.</summary>
    Guid? PagoComRegraId = null);
