using Microsoft.Data.Sqlite;

namespace Financas.Tests;

/// <summary>Banco SQLite em memória com as migrations reais (inclusive triggers) e os serviços prontos.</summary>
public sealed class Ambiente : IDisposable
{
    private readonly SqliteConnection _conexao;
    public AppDbContext Db { get; }
    public RelogioFixo Relogio { get; } = new(new DateOnly(2026, 10, 15));
    public RegraService Regras { get; }
    public LancamentoService Lancamentos { get; }
    public CartaoService Cartoes { get; }
    public PlanejamentoService Planejamento { get; }
    public ResumoService Resumo { get; }
    public PrevisaoService Previsao { get; }
    public QuadroPrevisaoService Quadro { get; }
    public ConfiguracaoPrevisaoService ConfigPrevisao { get; }
    public RelatorioService Relatorios { get; }
    public ContaService Contas { get; }
    public RecargaDoVale Recargas { get; }

    public Conta Banco { get; }
    public Conta Poupanca { get; }
    public Conta Cartao { get; }
    public Categoria Mercado { get; }
    public Categoria Lazer { get; }
    public Categoria Energia { get; }
    public Categoria Salario { get; }

    public Ambiente()
    {
        _conexao = new SqliteConnection("Data Source=:memory:");
        _conexao.Open();
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options);
        Db.Database.Migrate();

        var saldos = new SaldoService(Db);
        Regras = new RegraService(Db, Relogio, new GeradorDeOcorrencias(Db, Relogio));
        Lancamentos = new LancamentoService(Db, Relogio, Regras, saldos);
        Cartoes = new CartaoService(Db, Relogio);
        Planejamento = new PlanejamentoService(Db, Relogio, Regras);
        var dias = new DiasDePagamento(Db);
        var preferencias = new PreferenciasPrevisao(Db);
        Previsao = new PrevisaoService(Db, Relogio, Regras, Cartoes, dias, preferencias);
        Quadro = new QuadroPrevisaoService(Db, Cartoes);
        ConfigPrevisao = new ConfiguracaoPrevisaoService(preferencias, dias, Previsao);
        Resumo = new ResumoService(Db, Relogio, saldos, Planejamento, Cartoes, Previsao);
        Relatorios = new RelatorioService(Db);
        Recargas = new RecargaDoVale(Db, Relogio, Regras);
        Contas = new ContaService(Db, Relogio, saldos, Cartoes, Recargas);

        Banco = new Conta { Nome = "Banco", Tipo = TipoConta.Corrente, SaldoInicial = 1000m };
        Poupanca = new Conta { Nome = "Poupança", Tipo = TipoConta.Poupanca };
        Cartao = new Conta { Nome = "Cartão", Tipo = TipoConta.CartaoCredito, DiaFechamento = 3, DiaVencimento = 10 };
        Mercado = new Categoria { Nome = "Mercado", Tipo = TipoCategoria.Despesa };
        Lazer = new Categoria { Nome = "Lazer", Tipo = TipoCategoria.Despesa };
        Energia = new Categoria { Nome = "Energia", Tipo = TipoCategoria.Despesa };
        Salario = new Categoria { Nome = "Salário", Tipo = TipoCategoria.Receita };
        Db.AddRange(Banco, Poupanca, Cartao, Mercado, Lazer, Energia, Salario);
        Db.SaveChanges();
    }

    public async Task<Lancamento> Despesa(decimal valor, DateOnly data, Categoria? cat = null, Conta? conta = null,
        StatusLancamento? status = null, int? parcelas = null) =>
        (await Lancamentos.Criar(new NovoLancamentoDto(TipoLancamento.Despesa, "Despesa", valor, data,
            (conta ?? Banco).Id, null, (cat ?? Mercado).Id, FormaPagamento.Pix, null, status, parcelas, null))).First();

    public Task<Regra> Recorrente(TipoLancamento tipo, Categoria cat, decimal valor, DateOnly inicio, Conta? conta = null,
        FormaPagamento forma = FormaPagamento.Boleto) =>
        Regras.Criar(new RegraDto(ModoRegra.Recorrente, tipo, cat.Nome, cat.Id, (conta ?? Banco).Id, forma, valor,
            false, null, Frequencia.Mensal, 1, inicio, null, null));

    /// <summary>Entrada recorrente na categoria Salário (vira dia de pagamento da previsão).</summary>
    public Task<Regra> Pagamento(string nome, decimal valor, DateOnly inicio, RegraVencimento? venc = null, int? diaUtil = null) =>
        Regras.Criar(new RegraDto(ModoRegra.Recorrente, TipoLancamento.Receita, nome, Salario.Id, Banco.Id, FormaPagamento.Pix,
            valor, false, null, Frequencia.Mensal, 1, inicio, null, null, venc, diaUtil));

    public static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    public void Dispose()
    {
        Db.Dispose();
        _conexao.Dispose();
    }
}
