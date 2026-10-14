using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Servicos;

public enum TipoItemPrevisao { Entrada, Conta, Fatura }

public record ItemPrevisao(
    TipoItemPrevisao Tipo,
    string Descricao,
    string? Detalhe,
    string? Cor,
    string? Icone,
    DateOnly Data,
    decimal Valor,
    decimal Pago,
    bool Pendente,
    bool Atrasado,
    Guid? LancamentoId,
    Guid? RegraId,
    Guid? CartaoId,
    DateOnly? FaturaMes,
    decimal? Juros);

public record PeriodoPrevisao(
    DateOnly Inicio,
    DateOnly Fim,
    string Titulo,
    bool Atual,
    bool Encerrado,
    decimal SaldoInicio,
    decimal SaldoFim,
    decimal Entradas,
    decimal AReceber,
    decimal Contas,
    decimal ContasAPagar,
    decimal Faturas,
    decimal FaturasAPagar,
    decimal DiaADia,
    int DiasRestantes,
    decimal? PorDia,
    List<ItemPrevisao> Itens);

public record PrevisaoView(
    DateOnly Hoje,
    DateOnly Inicio,
    DateOnly Fim,
    /// <summary>false = sem salário cadastrado: o ciclo vira o mês do calendário.</summary>
    bool PorSalario,
    List<PeriodoPrevisao> Periodos,
    decimal SaldoInicio,
    decimal SaldoFim,
    decimal Entradas,
    decimal Contas,
    decimal Faturas,
    decimal DiaADia);

/// <summary>
/// Previsão em regime de caixa, por ciclo de salário: do pagamento principal (o maior, ex.: 5º dia útil)
/// até o próximo, dividido em períodos a cada outro pagamento (ex.: adiantamento do dia 20).
/// Só conta o dinheiro que paga contas (sem cartões nem vales). Compras no cartão entram pela fatura,
/// no período em que ela vence. Saldo de cada período:
///   - datas passadas: saldo real;
///   - de hoje em diante: saldo de hoje + entradas pendentes − contas pendentes − faturas em aberto até a data
///     (atrasados inclusive). Gastos do dia a dia futuros não são estimados: o saldo final é o que fica livre para eles.
/// </summary>
public class PrevisaoService(AppDbContext db, IRelogio relogio, RegraService regras, CartaoService cartoes)
{
    private DateOnly Hoje => relogio.Hoje;

    private record DiaPagamento(DateOnly Data, string Nome, bool Principal);

    public async Task<PrevisaoView> Obter(DateOnly? data = null)
    {
        var referencia = data ?? Hoje;
        var pagamentos = await DiasDePagamento(referencia.AddMonths(-3), referencia.AddMonths(3));
        var principais = pagamentos.Where(p => p.Principal).Select(p => p.Data).Distinct().Order().ToList();

        DateOnly inicio, fim;
        var porSalario = principais.Any(p => p <= referencia) && principais.Any(p => p > referencia);
        if (porSalario)
        {
            inicio = principais.Last(p => p <= referencia);
            fim = principais.First(p => p > referencia).AddDays(-1);
        }
        else
        {
            inicio = Calendario.InicioMes(referencia);
            fim = Calendario.FimMes(referencia);
        }

        await regras.Sincronizar(fim);

        // Fronteiras dos períodos: o início do ciclo e cada outro pagamento dentro dele.
        var cortes = pagamentos.Where(p => p.Data > inicio && p.Data <= fim).Select(p => p.Data).Distinct().Order().ToList();
        var inicios = new List<DateOnly> { inicio };
        if (porSalario) inicios.AddRange(cortes);

        var dados = await Carregar(inicio.AddMonths(-24), fim);
        var periodos = new List<PeriodoPrevisao>();
        for (var i = 0; i < inicios.Count; i++)
        {
            var pIni = inicios[i];
            var pFim = i + 1 < inicios.Count ? inicios[i + 1].AddDays(-1) : fim;
            var nomes = pagamentos.Where(p => p.Data == pIni).Select(p => p.Nome).Distinct().ToList();
            var titulo = nomes.Count > 0 ? string.Join(" + ", nomes) : porSalario ? "Período" : "Mês";
            periodos.Add(MontarPeriodo(dados, pIni, pFim, titulo));
        }

        return new PrevisaoView(Hoje, inicio, fim, porSalario, periodos,
            periodos[0].SaldoInicio, periodos[^1].SaldoFim,
            periodos.Sum(p => p.Entradas), periodos.Sum(p => p.Contas), periodos.Sum(p => p.Faturas), periodos.Sum(p => p.DiaADia));
    }

    /// <summary>
    /// Datas de pagamento (receitas recorrentes marcadas como salário) entre as datas. Usa a data real quando a
    /// ocorrência já existe e, antes do início da regra, a data que ela teria (só para marcar onde começa o ciclo).
    /// </summary>
    private async Task<List<DiaPagamento>> DiasDePagamento(DateOnly de, DateOnly ate)
    {
        var candidatas = await db.Regras.Include(r => r.Categoria).Include(r => r.Conta)
            .Where(r => r.Ativa && r.Modo == ModoRegra.Recorrente && r.Tipo == TipoLancamento.Receita)
            .ToListAsync();
        var salarios = candidatas
            .Where(r => r.Conta!.EhDisponivel && (r.IniciaPeriodo ?? EhSalario(r.Categoria!.Nome)))
            .ToList();
        if (salarios.Count == 0) return [];

        var principal = salarios.OrderByDescending(r => r.Valor).ThenBy(r => r.DataInicio.Day).First();
        var ids = salarios.Select(r => r.Id).ToList();
        var reais = await db.Lancamentos
            .Where(l => l.RegraId != null && ids.Contains(l.RegraId.Value) && l.Status != StatusLancamento.Cancelado)
            .Select(l => new { RegraId = l.RegraId!.Value, l.Sequencia, l.Data })
            .ToListAsync();

        var dias = new List<DiaPagamento>();
        foreach (var r in salarios)
        {
            for (var n = -60; n < 2000; n++)
            {
                var prevista = Calendario.Ocorrencia(r, n);
                if (prevista > ate.AddMonths(1) || (r.DataFim is { } f && prevista > f)) break;
                var data = reais.FirstOrDefault(x => x.RegraId == r.Id && x.Sequencia == n)?.Data ?? prevista;
                if (data >= de && data <= ate) dias.Add(new DiaPagamento(data, r.Descricao, r.Id == principal.Id));
            }
        }
        return dias.OrderBy(d => d.Data).ToList();
    }

    public static bool EhSalario(string categoria) =>
        categoria.Trim().StartsWith("salário", StringComparison.OrdinalIgnoreCase)
        || categoria.Trim().StartsWith("salario", StringComparison.OrdinalIgnoreCase);

    private record Dados(
        decimal SaldoHoje,
        Func<DateOnly, decimal> SaldoReal,
        List<Lancamento> Lancamentos,
        List<(FaturaView Fatura, string? Cor)> Faturas,
        HashSet<Guid> Disponiveis);

    private async Task<Dados> Carregar(DateOnly de, DateOnly ate)
    {
        var contas = await db.Contas.ToListAsync();
        var disponiveis = contas.Where(c => c.EhDisponivel).Select(c => c.Id).ToHashSet();
        var saldoInicial = contas.Where(c => c.EhDisponivel).Sum(c => c.SaldoInicial);

        var movimentos = await db.Lancamentos.Where(l => l.Status == StatusLancamento.Realizado)
            .Select(l => new { l.Tipo, l.Valor, l.ContaId, l.ContaDestinoId, l.Data })
            .ToListAsync();
        // Efeito de cada movimento no dinheiro disponível (transferência entre contas disponíveis = zero).
        var efeitos = movimentos.Select(m =>
        {
            var origem = disponiveis.Contains(m.ContaId);
            var destino = m.ContaDestinoId is { } d && disponiveis.Contains(d);
            var v = m.Tipo switch
            {
                TipoLancamento.Receita => origem ? m.Valor : 0,
                TipoLancamento.Despesa => origem ? -m.Valor : 0,
                _ => (destino ? m.Valor : 0) - (origem ? m.Valor : 0),
            };
            return (m.Data, v);
        }).Where(e => e.v != 0).ToList();

        var saldoHoje = saldoInicial + efeitos.Where(e => e.Data <= Hoje).Sum(e => e.v);
        decimal SaldoReal(DateOnly d) => saldoInicial + efeitos.Where(e => e.Data <= d).Sum(e => e.v);

        var lancamentos = await db.Lancamentos.Include(l => l.Categoria).Include(l => l.Conta)
            .Where(l => l.Status != StatusLancamento.Cancelado && l.Tipo != TipoLancamento.Transferencia
                        && l.Data <= ate && (l.Data >= de || l.Status == StatusLancamento.Previsto))
            .OrderBy(l => l.Data).ThenBy(l => l.CriadoEm)
            .ToListAsync();
        lancamentos = lancamentos.Where(l => disponiveis.Contains(l.ContaId)).ToList();

        var faturas = new List<(FaturaView, string?)>();
        foreach (var c in contas.Where(c => c.EhCartao && c.Ativa))
            faturas.AddRange((await cartoes.Faturas(c.Id)).Where(f => f.Total > 0 || f.Pago > 0).Select(f => (f, c.Cor)));

        return new Dados(saldoHoje, SaldoReal, lancamentos, faturas, disponiveis);
    }

    /// <summary>Saldo do dinheiro disponível no fim do dia informado (real no passado, projetado do hoje em diante).</summary>
    private decimal SaldoEm(Dados d, DateOnly data)
    {
        if (data < Hoje) return d.SaldoReal(data);
        var pendentes = d.Lancamentos.Where(l => l.Status == StatusLancamento.Previsto && l.Data <= data)
            .Sum(l => l.Tipo == TipoLancamento.Receita ? l.Valor : -l.Valor);
        var faturas = d.Faturas.Where(f => f.Fatura.Restante > 0 && f.Fatura.Vencimento <= data).Sum(f => f.Fatura.Restante);
        return d.SaldoHoje + pendentes - faturas;
    }

    private PeriodoPrevisao MontarPeriodo(Dados d, DateOnly inicio, DateOnly fim, string titulo)
    {
        var atual = inicio <= Hoje && Hoje <= fim;
        // O período de hoje também carrega o que ficou para trás sem pagar.
        bool Entra(DateOnly data, bool pendente) => (data >= inicio && data <= fim) || (atual && pendente && data < inicio);

        var itens = new List<ItemPrevisao>();
        foreach (var l in d.Lancamentos.Where(l => l.EstornoDeId is null))
        {
            var pendente = l.Status == StatusLancamento.Previsto;
            if (!Entra(l.Data, pendente)) continue;
            if (l.Tipo == TipoLancamento.Despesa && !l.Programado) continue;
            itens.Add(new ItemPrevisao(
                l.Tipo == TipoLancamento.Receita ? TipoItemPrevisao.Entrada : TipoItemPrevisao.Conta,
                l.Descricao, l.Categoria?.Nome, l.Categoria?.Cor, l.Categoria?.Icone, l.Data, l.Valor,
                pendente ? 0 : l.Valor, pendente, pendente && l.Data < Hoje,
                l.Id, l.RegraId, null, null, l.Juros));
        }
        foreach (var (f, cor) in d.Faturas)
        {
            var pendente = f.Restante > 0;
            if (!Entra(f.Vencimento, pendente)) continue;
            itens.Add(new ItemPrevisao(TipoItemPrevisao.Fatura, $"Fatura {f.Cartao}", StatusTexto(f.Status), cor, "mdi-credit-card-outline",
                f.Vencimento, f.Total, f.Pago, pendente, pendente && f.Vencimento < Hoje, null, null, f.CartaoId, f.Mes, null));
        }
        itens = itens.OrderBy(i => i.Data).ThenBy(i => i.Tipo).ToList();

        var diaADia = d.Lancamentos
            .Where(l => l.Tipo == TipoLancamento.Despesa && !l.Programado && l.Status == StatusLancamento.Realizado
                        && l.Data >= inicio && l.Data <= fim)
            .Sum(l => l.Valor);

        var saldoInicio = SaldoEm(d, inicio.AddDays(-1));
        var saldoFim = SaldoEm(d, fim);
        var dias = fim < Hoje ? 0 : fim.DayNumber - (inicio > Hoje ? inicio : Hoje).DayNumber + 1;

        var entradas = itens.Where(i => i.Tipo == TipoItemPrevisao.Entrada).ToList();
        var contas = itens.Where(i => i.Tipo == TipoItemPrevisao.Conta).ToList();
        var faturas = itens.Where(i => i.Tipo == TipoItemPrevisao.Fatura).ToList();
        return new PeriodoPrevisao(inicio, fim, titulo, atual, fim < Hoje, saldoInicio, saldoFim,
            entradas.Sum(i => i.Valor), entradas.Where(i => i.Pendente).Sum(i => i.Valor),
            contas.Sum(i => i.Valor), contas.Where(i => i.Pendente).Sum(i => i.Valor),
            faturas.Sum(i => i.Valor), faturas.Sum(i => i.Valor - i.Pago),
            diaADia, dias, dias > 0 && saldoFim > 0 ? decimal.Round(saldoFim / dias, 2) : null, itens);
    }

    private static string StatusTexto(StatusFatura s) => s switch
    {
        StatusFatura.Aberta => "Aberta (ainda recebe compras)",
        StatusFatura.Fechada => "Fechada",
        StatusFatura.Paga => "Paga",
        StatusFatura.Vencida => "Vencida",
        _ => "Futura",
    };
}
