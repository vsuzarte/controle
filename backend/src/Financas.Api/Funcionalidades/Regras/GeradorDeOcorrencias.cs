using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Regras;

/// <summary>
/// Cria os lançamentos previstos de uma regra (contas do mês, salário, parcelas). Idempotente:
/// cada ocorrência tem um número de sequência único por regra, então rodar de novo nunca duplica,
/// e uma ocorrência cancelada não volta a ser criada. Só adiciona ao contexto; quem chama grava.
/// </summary>
public class GeradorDeOcorrencias(AppDbContext db, IRelogio relogio)
{
    private DateOnly Hoje => relogio.Hoje;

    public async Task Gerar(Regra regra, DateOnly horizonte)
    {
        if (!regra.Ativa) return;
        var conta = await db.Contas.FindAsync(regra.ContaId) ?? throw new RegraNegocioException("Conta não encontrada.");

        var existentes = db.Entry(regra).State == EntityState.Added
            ? new HashSet<int>()
            : (await db.Lancamentos.Where(l => l.RegraId == regra.Id).Select(l => l.Sequencia ?? 0).ToListAsync()).ToHashSet();

        if (regra.Modo == ModoRegra.Parcelado)
        {
            var n = regra.Parcelas!.Value;
            var parcelas = DividirParcelas(regra.Valor, n);
            var primeiraFatura = conta.EhCartao ? Calendario.FaturaDaCompra(conta, regra.DataInicio) : (DateOnly?)null;
            for (var i = 1; i <= n; i++)
            {
                if (existentes.Contains(i)) continue;
                var data = Calendario.Ocorrencia(regra, i);
                db.Lancamentos.Add(NovaOcorrencia(regra, conta, i, data, parcelas[i - 1], primeiraFatura?.AddMonths(i - 1)));
            }
            return;
        }

        for (var i = 1; i < 2000; i++)
        {
            var data = Calendario.Ocorrencia(regra, i);
            if (data > horizonte || (regra.DataFim is { } fim && data > fim)) break;
            if (existentes.Contains(i)) continue;
            var fatura = conta.EhCartao ? Calendario.FaturaDaCompra(conta, data) : (DateOnly?)null;
            db.Lancamentos.Add(NovaOcorrencia(regra, conta, i, data, regra.Valor, fatura));
        }
    }

    /// <summary>"Aluguel" ou, nas parcelas, "TV (3/10)".</summary>
    public static string Descricao(Regra regra, int seq) =>
        regra.Modo == ModoRegra.Parcelado ? $"{regra.Descricao} ({seq}/{regra.Parcelas})" : regra.Descricao;

    /// <summary>Divide em parcelas iguais; a diferença de centavos vai na primeira, e a soma fecha exatamente com o total.</summary>
    public static decimal[] DividirParcelas(decimal total, int n)
    {
        var baseParcela = Math.Floor(total * 100m / n) / 100m;
        var parcelas = Enumerable.Repeat(baseParcela, n).ToArray();
        parcelas[0] += total - baseParcela * n;
        return parcelas;
    }

    private Lancamento NovaOcorrencia(Regra regra, Conta conta, int seq, DateOnly data, decimal valor, DateOnly? fatura) => new()
    {
        Tipo = regra.Tipo,
        // Compra parcelada no cartão já está comprometida: parcelas passadas nascem realizadas.
        Status = regra.Modo == ModoRegra.Parcelado && conta.EhCartao && data <= Hoje
            ? StatusLancamento.Realizado
            : StatusLancamento.Previsto,
        Descricao = Descricao(regra, seq),
        Valor = valor,
        Data = data,
        ContaId = conta.Id,
        CategoriaId = regra.CategoriaId,
        FormaPagamento = conta.EhCartao ? FormaPagamento.CartaoCredito : regra.FormaPagamento,
        Observacao = regra.Observacao,
        Programado = true,
        RegraId = regra.Id,
        Sequencia = seq,
        FaturaMes = fatura,
    };
}
