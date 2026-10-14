using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Servicos;

public record SaldoConta(Guid Id, string Nome, TipoConta Tipo, string? Cor, decimal Saldo);

public enum TipoAlerta { Vencido, Hoje, Proximo }

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
    Guid? RegraId = null);

/// <summary>Vale-refeição/alimentação: saldo, próxima recarga e até quando dura no ritmo dos últimos 30 dias.</summary>
public record BeneficioView(Guid Id, string Nome, string? Cor, decimal Saldo, DateOnly? ProximaRecarga, decimal? ValorRecarga,
    decimal GastoPorDia, DateOnly? DuraAte);

/// <summary>Gasto = tudo da categoria no mês; Variavel = só o do dia a dia (o que o orçamento controla).</summary>
public record GastoCategoria(Guid CategoriaId, string Categoria, string? Cor, decimal Gasto, decimal Variavel, decimal? Orcado);

public record ResumoView(
    DateOnly Hoje,
    decimal SaldoDisponivel,
    List<SaldoConta> Contas,
    decimal EntradasMes,
    decimal SaidasMes,
    List<Alerta> Alertas,
    List<FaturaView> FaturasAbertas,
    decimal ReceitasAReceber,
    decimal DespesasAPagar,
    decimal FaturasAPagar,
    decimal VariavelRestante,
    decimal SaldoProjetado,
    PlanejamentoView Planejamento,
    List<GastoCategoria> GastosPorCategoria,
    PrevisaoView Previsao,
    List<BeneficioView> Beneficios);

public class ResumoService(AppDbContext db, IRelogio relogio, PlanejamentoService planejamento, CartaoService cartoes, PrevisaoService previsoes)
{
    public const int DiasAlerta = 7;

    public async Task<ResumoView> Obter()
    {
        var hoje = relogio.Hoje;
        var inicioMes = Calendario.InicioMes(hoje);
        var fimMes = Calendario.FimMes(hoje);

        // Planejamento primeiro: ele também sincroniza as recorrências.
        var plano = await planejamento.Obter(inicioMes);

        var contas = await db.Contas.Where(c => c.Ativa).OrderBy(c => c.Tipo).ThenBy(c => c.Nome).ToListAsync();
        var saldos = await Saldos(hoje);
        var saldoContas = contas
            .Select(c => new SaldoConta(c.Id, c.Nome, c.Tipo, c.Cor, c.SaldoInicial + saldos.GetValueOrDefault(c.Id)))
            .ToList();
        var idsDisponiveis = contas.Where(c => c.EhDisponivel).Select(c => c.Id).ToHashSet();
        var saldoDisponivel = saldoContas.Where(c => idsDisponiveis.Contains(c.Id)).Sum(c => c.Saldo);
        var previsao = await previsoes.Obter();
        // Vencimentos: tudo o que está pendente até o próximo salário (no mínimo os próximos 7 dias).
        var limiteAlertas = previsao.Fim > hoje.AddDays(DiasAlerta) ? previsao.Fim : hoje.AddDays(DiasAlerta);

        var doMes = await db.Lancamentos.Include(l => l.Categoria)
            .Where(l => l.Status == StatusLancamento.Realizado && l.Tipo != TipoLancamento.Transferencia
                        && l.Data >= inicioMes && l.Data <= fimMes)
            .ToListAsync();

        // Pendências no dinheiro disponível (sem cartão e vale): o que ainda vai sair/entrar do saldo.
        var todosPendentes = await db.Lancamentos.Include(l => l.Conta)
            .Where(l => l.Status == StatusLancamento.Previsto && l.Data <= (limiteAlertas > fimMes ? limiteAlertas : fimMes))
            .OrderBy(l => l.Data)
            .ToListAsync();
        todosPendentes = todosPendentes.Where(l => l.Conta!.EhDisponivel).ToList();
        var pendentes = todosPendentes.Where(l => l.Data <= fimMes).ToList();

        var alertas = todosPendentes
            .Where(l => l.Data <= limiteAlertas && l.Tipo != TipoLancamento.Transferencia)
            .Select(l => NovoAlerta(hoje, l.Data, l.Descricao, l.Valor, l.Tipo, l.Id, null, null) with { RegraId = l.RegraId })
            .ToList();

        var faturasAbertas = new List<FaturaView>();
        decimal faturasAPagar = 0;
        foreach (var cartao in contas.Where(c => c.EhCartao))
        {
            var faturas = await cartoes.Faturas(cartao.Id);
            foreach (var f in faturas.Where(f => f.Restante > 0 || f.Status == StatusFatura.Aberta))
            {
                if (f.Vencimento <= fimMes) faturasAPagar += f.Restante;
                if (f.Restante > 0 && f.Vencimento <= limiteAlertas)
                    alertas.Add(NovoAlerta(hoje, f.Vencimento, $"Fatura {cartao.Nome} {f.Mes:MM/yyyy}", f.Restante,
                        TipoLancamento.Despesa, null, cartao.Id, f.Mes));
                if (f.Status is StatusFatura.Aberta or StatusFatura.Fechada or StatusFatura.Vencida)
                    faturasAbertas.Add(f with { Lancamentos = [], Pagamentos = [] });
            }
        }

        var receitasAReceber = pendentes.Where(l => l.Tipo == TipoLancamento.Receita).Sum(l => l.Valor);
        var despesasAPagar = pendentes.Where(l => l.Tipo == TipoLancamento.Despesa).Sum(l => l.Valor);
        var variavelRestante = plano.Variaveis.Where(v => !v.Dispensado).Sum(v => Math.Max(0, (v.Orcado ?? 0) - v.Gasto));

        var gastos = doMes.Where(l => l.Tipo == TipoLancamento.Despesa)
            .GroupBy(l => l.CategoriaId!.Value)
            .Select(g =>
            {
                var c = g.First().Categoria!;
                var orcado = plano.Variaveis.FirstOrDefault(v => v.CategoriaId == c.Id && !v.Dispensado)?.Orcado;
                return new GastoCategoria(c.Id, c.Nome, c.Cor, g.Sum(l => l.Valor), g.Where(l => !l.Programado).Sum(l => l.Valor), orcado);
            })
            .OrderByDescending(g => g.Gasto)
            .ToList();

        return new ResumoView(
            hoje,
            saldoDisponivel,
            saldoContas,
            doMes.Where(l => l.Tipo == TipoLancamento.Receita).Sum(l => l.Valor),
            doMes.Where(l => l.Tipo == TipoLancamento.Despesa).Sum(l => l.Valor),
            alertas.OrderBy(a => a.Data).ToList(),
            faturasAbertas.OrderBy(f => f.Vencimento).ToList(),
            receitasAReceber,
            despesasAPagar,
            faturasAPagar,
            variavelRestante,
            saldoDisponivel + receitasAReceber - despesasAPagar - faturasAPagar - variavelRestante,
            plano,
            gastos,
            previsao,
            await Beneficios(contas.Where(c => c.EhBeneficio).ToList(), saldoContas, hoje));
    }

    private async Task<List<BeneficioView>> Beneficios(List<Conta> vales, List<SaldoConta> saldos, DateOnly hoje)
    {
        if (vales.Count == 0) return [];
        var ids = vales.Select(v => v.Id).ToList();
        var desde = hoje.AddDays(-30);
        var gastos = await db.Lancamentos
            .Where(l => ids.Contains(l.ContaId) && l.Tipo == TipoLancamento.Despesa && l.Status == StatusLancamento.Realizado
                        && l.Data > desde && l.Data <= hoje)
            .Select(l => new { l.ContaId, l.Valor }).ToListAsync();
        var recargas = await db.Lancamentos
            .Where(l => ids.Contains(l.ContaId) && l.Tipo == TipoLancamento.Receita && l.Status == StatusLancamento.Previsto && l.Data > hoje)
            .OrderBy(l => l.Data).Select(l => new { l.ContaId, l.Data, l.Valor }).ToListAsync();
        return vales.Select(v =>
        {
            var saldo = saldos.First(s => s.Id == v.Id).Saldo;
            var porDia = decimal.Round(gastos.Where(g => g.ContaId == v.Id).Sum(g => g.Valor) / 30m, 2);
            var recarga = recargas.FirstOrDefault(r => r.ContaId == v.Id);
            DateOnly? duraAte = porDia > 0 && saldo > 0 ? hoje.AddDays((int)Math.Floor(saldo / porDia)) : null;
            return new BeneficioView(v.Id, v.Nome, v.Cor, saldo, recarga?.Data, recarga?.Valor, porDia, duraAte);
        }).ToList();
    }

    /// <summary>Movimento líquido realizado de cada conta até a data (sem o saldo inicial).</summary>
    public async Task<Dictionary<Guid, decimal>> Saldos(DateOnly ate)
    {
        var movimentos = await db.Lancamentos
            .Where(l => l.Status == StatusLancamento.Realizado && l.Data <= ate)
            .Select(l => new { l.Tipo, l.Valor, l.ContaId, l.ContaDestinoId })
            .ToListAsync();

        var saldos = new Dictionary<Guid, decimal>();
        void Somar(Guid conta, decimal v) => saldos[conta] = saldos.GetValueOrDefault(conta) + v;
        foreach (var m in movimentos)
        {
            switch (m.Tipo)
            {
                case TipoLancamento.Receita: Somar(m.ContaId, m.Valor); break;
                case TipoLancamento.Despesa: Somar(m.ContaId, -m.Valor); break;
                case TipoLancamento.Transferencia:
                    Somar(m.ContaId, -m.Valor);
                    Somar(m.ContaDestinoId!.Value, m.Valor);
                    break;
            }
        }
        return saldos;
    }

    private static Alerta NovoAlerta(DateOnly hoje, DateOnly data, string descricao, decimal valor,
        TipoLancamento? tipo, Guid? lancamentoId, Guid? cartaoId, DateOnly? faturaMes)
    {
        var dias = data.DayNumber - hoje.DayNumber;
        var t = dias < 0 ? TipoAlerta.Vencido : dias == 0 ? TipoAlerta.Hoje : TipoAlerta.Proximo;
        return new Alerta(t, Math.Abs(dias), descricao, valor, data, tipo, lancamentoId, cartaoId, faturaMes);
    }
}
