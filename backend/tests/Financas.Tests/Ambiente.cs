using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Servicos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Financas.Tests;

public class RelogioFixo(DateOnly hoje) : IRelogio
{
    public DateOnly Hoje { get; set; } = hoje;
}

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

        Regras = new RegraService(Db, Relogio);
        Lancamentos = new LancamentoService(Db, Relogio, Regras);
        Cartoes = new CartaoService(Db, Relogio);
        Planejamento = new PlanejamentoService(Db, Relogio, Regras);
        Resumo = new ResumoService(Db, Relogio, Planejamento, Cartoes);

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

    public static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    public void Dispose()
    {
        Db.Dispose();
        _conexao.Dispose();
    }
}
