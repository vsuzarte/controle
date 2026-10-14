namespace Financas.Api.Funcionalidades.Previsao;

public record ItemPrevisao(
    TipoItemPrevisao Tipo,
    string Descricao,
    string? Detalhe,
    string? Cor,
    string? Icone,
    DateOnly Data,
    decimal Valor,
    decimal Pago,
    bool Pendente,
    bool Atrasado,
    Guid? LancamentoId,
    Guid? RegraId,
    Guid? CartaoId,
    DateOnly? FaturaMes,
    decimal? Juros,
    /// <summary>Vence em fim de semana/feriado: dá para pagar sem atraso até este dia útil (é ele que define o período).</summary>
    DateOnly? PagavelAte = null,
    /// <summary>Conta marcada para ser paga com um dia de pagamento: o nome dele e o dia em que entra o dinheiro.</summary>
    string? PagoCom = null,
    DateOnly? PagarEm = null,
    /// <summary>Quantos dias depois do prazo sem juros a conta seria paga com esse dinheiro (risco de multa).</summary>
    int? DiasDepoisDoVencimento = null,
    /// <summary>Movida à mão no quadro (só esta ocorrência).</summary>
    bool Movido = false);
