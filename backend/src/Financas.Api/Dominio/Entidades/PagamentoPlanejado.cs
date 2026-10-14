namespace Financas.Api.Dominio;

/// <summary>
/// Planejamento feito no quadro da previsão: pagar esta conta (uma ocorrência) ou esta fatura em outra data,
/// ou seja, com o dinheiro de outro período. Não altera o lançamento: valor, vencimento e saldo continuam iguais.
/// </summary>
public class PagamentoPlanejado
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Conta (ocorrência de uma conta do mês ou previsto avulso).</summary>
    public Guid? LancamentoId { get; set; }
    /// <summary>Ou fatura: cartão + mês da fatura.</summary>
    public Guid? CartaoId { get; set; }
    public DateOnly? FaturaMes { get; set; }
    public DateOnly PagarEm { get; set; }
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
