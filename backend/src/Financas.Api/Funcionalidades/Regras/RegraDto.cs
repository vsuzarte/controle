using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Regras;

public record RegraDto(
    ModoRegra Modo,
    TipoLancamento Tipo,
    string Descricao,
    Guid CategoriaId,
    Guid ContaId,
    FormaPagamento? FormaPagamento,
    decimal Valor,
    bool ValorVariavel,
    int? Parcelas,
    Frequencia Frequencia,
    int Intervalo,
    DateOnly DataInicio,
    DateOnly? DataFim,
    string? Observacao,
    RegraVencimento? Vencimento = null,
    int? DiaUtilNumero = null,
    bool? IniciaPeriodo = null,
    Guid? PagoComRegraId = null);
