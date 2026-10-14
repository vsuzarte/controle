using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Lancamentos;

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
    bool ValorVariavel,
    /// <summary>Veio de uma nota fiscal: os itens estão em /compras/{CompraId}.</summary>
    Guid? CompraId,
    /// <summary>Só na busca: os itens da nota que batem com o texto buscado (ex.: a Coca dentro da compra do mercado).</summary>
    List<ItemEncontradoView>? ItensEncontrados = null)
{
    public static LancamentoView De(Lancamento l) => new(
        l.Id, l.Tipo, l.Status, l.Descricao, l.Valor, l.Data,
        l.ContaId, l.Conta?.Nome ?? "", l.ContaDestinoId, l.ContaDestino?.Nome,
        l.CategoriaId, l.Categoria?.Nome, l.Categoria?.Cor, l.Categoria?.Icone,
        l.FormaPagamento, l.Observacao, l.Programado, l.RegraId, l.Sequencia,
        l.Regra?.Modo == ModoRegra.Parcelado ? l.Regra.Parcelas : null,
        l.FaturaMes, l.EstornoDeId, l.MotivoCancelamento, l.CriadoEm, l.AtualizadoEm, l.Juros, l.Regra?.ValorVariavel ?? false, l.CompraId);
}
