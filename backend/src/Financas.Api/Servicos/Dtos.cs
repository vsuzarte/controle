using Financas.Api.Dominio;

namespace Financas.Api.Servicos;

public record ContaDto(string Nome, TipoConta Tipo, decimal SaldoInicial, string? Cor, int? DiaFechamento, int? DiaVencimento, bool Ativa = true, decimal? Limite = null);

public record LimiteDto(decimal? Limite, string? Motivo);

public record CategoriaDto(string Nome, TipoCategoria Tipo, string? Grupo, string? Cor, string? Icone, bool Ativa = true);

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

/// <summary>Juros: quanto do valor pago foi juros/multa por atraso.</summary>
public record ConfirmarDto(DateOnly Data, decimal Valor, Guid? ContaId, FormaPagamento? FormaPagamento, decimal? Juros = null);
public record MotivoDto(string? Motivo);
public record EstornoDto(decimal Valor, DateOnly Data, string? Motivo);

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
    bool? IniciaPeriodo = null);

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
    bool? IniciaPeriodo = null);

public record DesativarRegraDto(bool CancelarPrevistos, string? Motivo);

/// <summary>Juros: parte do valor que foi juros/multa (vira uma despesa separada em "Tarifas e juros").</summary>
/// <summary>
/// Regra da janela da previsão e dos vencimentos da tela inicial.
/// AteAntesDoPagamento: mostrar tudo que vence até antes do N-ésimo próximo pagamento (nulo = padrão: um mês de pagamentos).
/// </summary>
public record ConfigPrevisaoDto(int? AteAntesDoPagamento, bool PularQuitado);

/// <summary>Configuração atual mais o que ajuda a entender o efeito: o padrão e até que data a janela vai hoje.</summary>
public record ConfigPrevisaoView(int? AteAntesDoPagamento, bool PularQuitado, int Padrao, int PagamentosPorMes, DateOnly? AteHoje);

public record PeriodoSalvoDto(string Nome, DateOnly De, DateOnly Ate);

public record PagarFaturaDto(Guid ContaOrigemId, decimal Valor, DateOnly Data, decimal? Juros = null);

public record ValorOrcamentoDto(decimal Valor, string? Motivo);

public record LancamentoView(
    Guid Id,
    TipoLancamento Tipo,
    StatusLancamento Status,
    string Descricao,
    decimal Valor,
    DateOnly Data,
    Guid ContaId,
    string Conta,
    Guid? ContaDestinoId,
    string? ContaDestino,
    Guid? CategoriaId,
    string? Categoria,
    string? CategoriaCor,
    string? CategoriaIcone,
    FormaPagamento? FormaPagamento,
    string? Observacao,
    bool Programado,
    Guid? RegraId,
    int? Sequencia,
    int? TotalParcelas,
    DateOnly? FaturaMes,
    Guid? EstornoDeId,
    string? MotivoCancelamento,
    DateTime CriadoEm,
    DateTime AtualizadoEm,
    decimal? Juros,
    /// <summary>Veio de uma conta do mês com valor que muda (luz, água): pagar mais não é juros.</summary>
    bool ValorVariavel)
{
    public static LancamentoView De(Lancamento l) => new(
        l.Id, l.Tipo, l.Status, l.Descricao, l.Valor, l.Data,
        l.ContaId, l.Conta?.Nome ?? "", l.ContaDestinoId, l.ContaDestino?.Nome,
        l.CategoriaId, l.Categoria?.Nome, l.Categoria?.Cor, l.Categoria?.Icone,
        l.FormaPagamento, l.Observacao, l.Programado, l.RegraId, l.Sequencia,
        l.Regra?.Modo == ModoRegra.Parcelado ? l.Regra.Parcelas : null,
        l.FaturaMes, l.EstornoDeId, l.MotivoCancelamento, l.CriadoEm, l.AtualizadoEm, l.Juros, l.Regra?.ValorVariavel ?? false);
}
