namespace Financas.Api.Dominio;

public class Conta : IAuditavel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Nome { get; set; } = "";
    public TipoConta Tipo { get; set; }
    public decimal SaldoInicial { get; set; }
    public string? Cor { get; set; }
    /// <summary>Somente cartão: dia em que a fatura fecha.</summary>
    public int? DiaFechamento { get; set; }
    /// <summary>Somente cartão: dia de vencimento da fatura.</summary>
    public int? DiaVencimento { get; set; }
    /// <summary>Somente cartão: limite de crédito atual. Pode ser ajustado a qualquer momento (fica na auditoria).</summary>
    public decimal? Limite { get; set; }
    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public bool EhCartao => Tipo == TipoConta.CartaoCredito;
    public bool EhBeneficio => Tipo == TipoConta.Beneficio;
    /// <summary>Dinheiro que paga contas: tudo menos cartão e vale.</summary>
    public bool EhDisponivel => !EhCartao && !EhBeneficio;
}
