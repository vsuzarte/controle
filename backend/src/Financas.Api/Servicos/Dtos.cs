using Financas.Api.Dominio;

namespace Financas.Api.Servicos;

public record ContaDto(string Nome, TipoConta Tipo, decimal SaldoInicial, string? Cor, int? DiaFechamento, int? DiaVencimento, bool Ativa = true);

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
    DateOnly? FaturaMes);

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

public record ConfirmarDto(DateOnly Data, decimal Valor, Guid? ContaId, FormaPagamento? FormaPagamento);
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
    string? Observacao);

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
    string? Motivo);

public record DesativarRegraDto(bool CancelarPrevistos, string? Motivo);

public record PagarFaturaDto(Guid ContaOrigemId, decimal Valor, DateOnly Data);

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
    DateTime AtualizadoEm)
{
    public static LancamentoView De(Lancamento l) => new(
        l.Id, l.Tipo, l.Status, l.Descricao, l.Valor, l.Data,
        l.ContaId, l.Conta?.Nome ?? "", l.ContaDestinoId, l.ContaDestino?.Nome,
        l.CategoriaId, l.Categoria?.Nome, l.Categoria?.Cor, l.Categoria?.Icone,
        l.FormaPagamento, l.Observacao, l.Programado, l.RegraId, l.Sequencia,
        l.Regra?.Modo == ModoRegra.Parcelado ? l.Regra.Parcelas : null,
        l.FaturaMes, l.EstornoDeId, l.MotivoCancelamento, l.CriadoEm, l.AtualizadoEm);
}
