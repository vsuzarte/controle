using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Cartoes;
using Financas.Api.Funcionalidades.Contas;
using Financas.Api.Funcionalidades.Planejamento;
using Financas.Api.Funcionalidades.Previsao;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Resumo;

/// <summary>Tela inicial: saldos, vencimentos, faturas, vales, gastos do mês e a previsão até o próximo pagamento.</summary>
public class ResumoService(AppDbContext db, IRelogio relogio, SaldoService saldoService, PlanejamentoService planejamento,
    CartaoService cartoes, PrevisaoService previsoes)
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
        var saldos = await saldoService.Saldos(hoje);
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
            .Select(l => (l, pagavel: l.Tipo == TipoLancamento.Despesa ? Calendario.ProximoDiaUtil(l.Data) : l.Data))
            .Where(x => x.pagavel <= limiteAlertas && x.l.Tipo != TipoLancamento.Transferencia)
            .Select(x => NovoAlerta(hoje, x.pagavel, x.l.Descricao, x.l.Valor, x.l.Tipo, x.l.Id, null, null)
                with
            { RegraId = x.l.RegraId, VenceEm = x.pagavel != x.l.Data ? x.l.Data : null })
            .ToList();

        var faturasAbertas = new List<FaturaView>();
        decimal faturasAPagar = 0;
        foreach (var cartao in contas.Where(c => c.EhCartao))
        {
            var faturas = await cartoes.Faturas(cartao.Id);
            foreach (var f in faturas.Where(f => f.Restante > 0 || f.Status == StatusFatura.Aberta))
            {
                if (f.Vencimento <= fimMes) faturasAPagar += f.Restante;
                var pagavelFatura = Calendario.ProximoDiaUtil(f.Vencimento);
                if (f.Restante > 0 && pagavelFatura <= limiteAlertas)
                    alertas.Add(NovoAlerta(hoje, pagavelFatura, $"Fatura {cartao.Nome} {f.Mes:MM/yyyy}", f.Restante,
                        TipoLancamento.Despesa, null, cartao.Id, f.Mes) with
                    { VenceEm = pagavelFatura != f.Vencimento ? f.Vencimento : null });
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

    private static Alerta NovoAlerta(DateOnly hoje, DateOnly data, string descricao, decimal valor,
        TipoLancamento? tipo, Guid? lancamentoId, Guid? cartaoId, DateOnly? faturaMes)
    {
        var dias = data.DayNumber - hoje.DayNumber;
        var t = dias < 0 ? TipoAlerta.Vencido : dias == 0 ? TipoAlerta.Hoje : TipoAlerta.Proximo;
        return new Alerta(t, Math.Abs(dias), descricao, valor, data, tipo, lancamentoId, cartaoId, faturaMes);
    }
}
