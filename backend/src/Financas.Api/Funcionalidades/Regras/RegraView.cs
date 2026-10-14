using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Regras;

/// <summary>Conta do mês / parcelamento com o andamento: quantas pagas, pendentes, atrasadas e a próxima.</summary>
public record RegraView(
    Guid Id,
    ModoRegra Modo,
    TipoLancamento Tipo,
    string Descricao,
    Guid CategoriaId,
    string Categoria,
    string? CategoriaCor,
    Guid ContaId,
    string Conta,
    FormaPagamento? FormaPagamento,
    decimal Valor,
    bool ValorVariavel,
    int? Parcelas,
    Frequencia Frequencia,
    int Intervalo,
    DateOnly DataInicio,
    DateOnly? DataFim,
    bool Ativa,
    string? Observacao,
    RegraVencimento Vencimento,
    int? DiaUtilNumero,
    Guid? PagoComRegraId,
    string? PagoCom,
    bool IniciaPeriodo,
    int Realizadas,
    int Pendentes,
    DateOnly? ProximaData,
    decimal? ProximoValor,
    int Atrasadas);
