namespace Financas.Api.Dominio;

public class Lancamento : IAuditavel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public TipoLancamento Tipo { get; set; }
    public StatusLancamento Status { get; set; }
    public string Descricao { get; set; } = "";
    /// <summary>Sempre positivo, exceto em estornos (negativo, reduzindo o lançamento original).</summary>
    public decimal Valor { get; set; }
    /// <summary>Data do vencimento (previsto) ou da efetivação (realizado).</summary>
    public DateOnly Data { get; set; }
    public Guid ContaId { get; set; }
    public Guid? ContaDestinoId { get; set; }
    public Guid? CategoriaId { get; set; }
    public FormaPagamento? FormaPagamento { get; set; }
    public string? Observacao { get; set; }

    /// <summary>
    /// Compromisso conhecido de antemão (veio de uma regra ou foi criado como previsto).
    /// Não entra na base de gastos variáveis usada para sugerir o orçamento.
    /// </summary>
    public bool Programado { get; set; }
    public Guid? RegraId { get; set; }
    /// <summary>Número da ocorrência/parcela dentro da regra (1..N). Único por regra: impede duplicatas ao regerar.</summary>
    public int? Sequencia { get; set; }

    /// <summary>Somente cartão: mês (dia 1) de vencimento da fatura a que o lançamento pertence.</summary>
    public DateOnly? FaturaMes { get; set; }

    /// <summary>Parte do valor pago que foi juros/multa por atraso (informada ao confirmar o pagamento).</summary>
    public decimal? Juros { get; set; }

    /// <summary>Veio de uma nota fiscal importada (os itens ficam na compra).</summary>
    public Guid? CompraId { get; set; }

    public Guid? EstornoDeId { get; set; }
    public string? MotivoCancelamento { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public Conta? Conta { get; set; }
    public Conta? ContaDestino { get; set; }
    public Categoria? Categoria { get; set; }
    public Regra? Regra { get; set; }

    public bool Valido => Status != StatusLancamento.Cancelado;
}
