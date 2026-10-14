namespace Financas.Api.Dominio;

/// <summary>Regra que gera lançamentos previstos: contas recorrentes, salário, compras parceladas.</summary>
public class Regra : IAuditavel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public ModoRegra Modo { get; set; }
    public TipoLancamento Tipo { get; set; }
    public string Descricao { get; set; } = "";
    public Guid CategoriaId { get; set; }
    public Guid ContaId { get; set; }
    public FormaPagamento? FormaPagamento { get; set; }
    /// <summary>Recorrente: valor de cada ocorrência. Parcelado: valor total da compra.</summary>
    public decimal Valor { get; set; }
    public bool ValorVariavel { get; set; }
    public int? Parcelas { get; set; }
    public Frequencia Frequencia { get; set; } = Frequencia.Mensal;
    /// <summary>Mensal: dia fixo (nulo), N-ésimo dia útil, último dia ou último dia útil do mês.</summary>
    public RegraVencimento? Vencimento { get; set; }
    /// <summary>Com Vencimento = DiaUtil: qual dia útil (ex.: 5 para "quinto dia útil").</summary>
    public int? DiaUtilNumero { get; set; }
    /// <summary>
    /// Receita que é dia de pagamento (salário, adiantamento): divide a previsão em períodos.
    /// Nulo = automático (receitas recorrentes da categoria Salário).
    /// </summary>
    public bool? IniciaPeriodo { get; set; }
    /// <summary>
    /// Só contas (despesas): com que dia de pagamento esta conta é paga (Id da entrada recorrente, ex.: salário).
    /// Nulo = automático, pelo vencimento. Usado só pela previsão.
    /// </summary>
    public Guid? PagoComRegraId { get; set; }
    public int Intervalo { get; set; } = 1;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public bool Ativa { get; set; } = true;
    public string? Observacao { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Categoria? Categoria { get; set; }
    public Conta? Conta { get; set; }
}
