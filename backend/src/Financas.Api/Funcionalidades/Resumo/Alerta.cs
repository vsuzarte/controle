using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Resumo;

public record Alerta(
    TipoAlerta Tipo,
    int Dias,
    string Descricao,
    decimal Valor,
    DateOnly Data,
    TipoLancamento? TipoLancamento,
    Guid? LancamentoId,
    Guid? CartaoId,
    DateOnly? FaturaMes,
    Guid? RegraId = null,
    /// <summary>Vencimento original quando cai em fim de semana/feriado (Data passa a ser o próximo dia útil).</summary>
    DateOnly? VenceEm = null);
