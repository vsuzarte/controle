namespace Financas.Api.Dominio;

public enum TipoConta { Corrente, Poupanca, Dinheiro, CarteiraDigital, CartaoCredito }
public enum TipoCategoria { Receita, Despesa }
public enum TipoLancamento { Receita, Despesa, Transferencia }
public enum StatusLancamento { Previsto, Realizado, Cancelado }
public enum FormaPagamento { Pix, Boleto, DebitoAutomatico, CartaoCredito, CartaoDebito, Dinheiro, Transferencia, Outro }
public enum ModoRegra { Recorrente, Parcelado }
public enum Frequencia { Semanal, Mensal, Anual }

/// <summary>Marca entidades cujas alterações vão para a trilha de auditoria e que nunca podem ser excluídas.</summary>
public interface IAuditavel
{
    Guid Id { get; }
}

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
    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public bool EhCartao => Tipo == TipoConta.CartaoCredito;
}

public class Categoria : IAuditavel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Nome { get; set; } = "";
    public TipoCategoria Tipo { get; set; }
    public string? Grupo { get; set; }
    public string? Cor { get; set; }
    public string? Icone { get; set; }
    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

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
    public int Intervalo { get; set; } = 1;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public bool Ativa { get; set; } = true;
    public string? Observacao { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Categoria? Categoria { get; set; }
    public Conta? Conta { get; set; }
}

/// <summary>Valor orçado para uma categoria de despesa variável num mês.</summary>
public class OrcamentoItem : IAuditavel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateOnly Mes { get; set; }
    public Guid CategoriaId { get; set; }
    public decimal Valor { get; set; }
    /// <summary>Valor calculado pelo sistema no momento da sugestão (referência).</summary>
    public decimal ValorSugerido { get; set; }
    public bool Editado { get; set; }
    /// <summary>Removido da previsão deste mês pelo usuário. Não reaparece sozinho.</summary>
    public bool Dispensado { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Categoria? Categoria { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public string Entidade { get; set; } = "";
    public Guid EntidadeId { get; set; }
    public string Operacao { get; set; } = "";
    public string? Antes { get; set; }
    public string? Depois { get; set; }
    public string? Motivo { get; set; }
    public DateTime DataHora { get; set; } = DateTime.UtcNow;
}

public class RegraNegocioException(string mensagem) : Exception(mensagem);
