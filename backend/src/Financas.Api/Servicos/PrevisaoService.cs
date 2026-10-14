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
    decimal? Juros,
    /// <summary>Vence em fim de semana/feriado: dá para pagar sem atraso até este dia útil (é ele que define o período).</summary>
    DateOnly? PagavelAte = null,
    /// <summary>Conta marcada para ser paga com um dia de pagamento: o nome dele e o dia em que entra o dinheiro.</summary>
    string? PagoCom = null,
    DateOnly? PagarEm = null,
    /// <summary>Quantos dias depois do prazo sem juros a conta seria paga com esse dinheiro (risco de multa).</summary>
    int? DiasDepoisDoVencimento = null);

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
    List<ItemPrevisao> Itens,
    /// <summary>Período de hoje sem nada pendente: aparece resumido e a previsão já mostra o seguinte.</summary>
    bool Quitado = false);

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
    decimal DiaADia,
    /// <summary>Intervalo escolhido pelo usuário (em vez da janela automática).</summary>
    bool Personalizado = false);

/// <summary>
/// Previsão em regime de caixa dividida em períodos de pagamento: cada pagamento (salário do 5º dia útil,
/// adiantamento do dia 20...) cobre as contas até o pagamento seguinte. A janela mostra o período de hoje e
/// mais um mês de pagamentos à frente (com dois pagamentos por mês: o atual e os dois seguintes), para planejar
/// desde já o que o próximo salário vai ter que pagar.
/// Só conta o dinheiro que paga contas (sem cartões nem vales). Compras no cartão entram pela fatura,
/// no período em que ela vence. Saldo de cada período:
///   - datas passadas: saldo real;
///   - de hoje em diante: saldo de hoje + entradas pendentes − contas pendentes − faturas em aberto até a data
///     (atrasados inclusive). Gastos do dia a dia futuros não são estimados: o saldo final é o que fica livre para eles.
/// </summary>
public class PrevisaoService(AppDbContext db, IRelogio relogio, RegraService regras, CartaoService cartoes)
{
    private DateOnly Hoje => relogio.Hoje;

    private record DiaPagamento(DateOnly Data, string Nome, Guid RegraId);

    /// <summary>
    /// Visão automática: o período de hoje e mais um mês de pagamentos à frente. Se o período de hoje já está
    /// todo pago e recebido, ele aparece como quitado e a janela avança um período.
    /// Com <paramref name="de"/> e <paramref name="ate"/>, mostra esse intervalo (dividido pelos pagamentos dentro dele).
    /// </summary>
    public async Task<PrevisaoView> Obter(DateOnly? data = null, DateOnly? de = null, DateOnly? ate = null)
    {
        if (de is { } d0 && ate is { } a0) return await Intervalo(d0, a0);

        var referencia = data ?? Hoje;
        var config = await Config();
        // Quantos pagamentos por mês vem do cadastro (Contas do mês), não do código. Padrão: um mês de pagamentos à frente.
        var porMes = await PagamentosPorMes();
        var ateAntesDo = config.AteAntesDoPagamento ?? porMes + 1;
        var pagamentos = await DiasDePagamento(referencia.AddMonths(-3), referencia.AddMonths(4 + ateAntesDo));
        var datas = pagamentos.Select(p => p.Data).Distinct().Order().ToList();
        var futuras = datas.Where(d => d > referencia).ToList();

        if (!datas.Any(d => d <= referencia) || futuras.Count < ateAntesDo + 1)
            return await Montar([Calendario.InicioMes(referencia)], Calendario.FimMes(referencia), pagamentos, porSalario: false, pularQuitados: false);

        var fronteiras = new List<DateOnly> { datas.Last(d => d <= referencia) };
        fronteiras.AddRange(futuras);
        // O período de hoje (talvez quitado) e mais N: monta um a mais para a janela poder avançar.
        var quantos = Math.Min(fronteiras.Count - 1, ateAntesDo + 1);
        return await Montar(fronteiras.Take(quantos).ToList(), fronteiras[quantos].AddDays(-1), pagamentos, porSalario: true,
            pularQuitados: data is null && config.PularQuitado, visiveis: ateAntesDo);
    }

    // ---------- Configuração da janela ----------

    private const string ChaveAteAntes = "Previsao.AteAntesDoPagamento";
    private const string ChavePularQuitado = "Previsao.PularQuitado";

    public async Task<ConfigPrevisaoDto> Config()
    {
        var c = await db.Configuracoes.Where(x => x.Chave.StartsWith("Previsao.")).ToDictionaryAsync(x => x.Chave, x => x.Valor);
        return new ConfigPrevisaoDto(
            c.TryGetValue(ChaveAteAntes, out var n) && int.TryParse(n, out var v) ? v : null,
            !c.TryGetValue(ChavePularQuitado, out var p) || p != "false");
    }

    public async Task<ConfigPrevisaoView> VerConfig()
    {
        var c = await Config();
        var porMes = await PagamentosPorMes();
        var hoje = await Obter();
        return new ConfigPrevisaoView(c.AteAntesDoPagamento, c.PularQuitado, porMes + 1, porMes, hoje.PorSalario ? hoje.Fim : null);
    }

    public async Task<ConfigPrevisaoView> SalvarConfig(ConfigPrevisaoDto dto)
    {
        if (dto.AteAntesDoPagamento is not null and not (>= 1 and <= 24))
            throw new RegraNegocioException("Escolha de 1 a 24 pagamentos à frente.");
        await Gravar(ChaveAteAntes, dto.AteAntesDoPagamento?.ToString());
        await Gravar(ChavePularQuitado, dto.PularQuitado ? "true" : "false");
        await db.SaveChangesAsync();
        return await VerConfig();
    }

    private async Task Gravar(string chave, string? valor)
    {
        var c = await db.Configuracoes.FindAsync(chave);
        if (valor is null) { if (c is not null) db.Configuracoes.Remove(c); return; }
        if (c is null) db.Configuracoes.Add(new Configuracao { Chave = chave, Valor = valor });
        else c.Valor = valor;
    }

    /// <summary>Quantas entradas marcadas como dia de pagamento estão ativas (cada uma = um pagamento por mês).</summary>
    private async Task<int> PagamentosPorMes() => Math.Max(1, (await RegrasDePagamento()).Count);

    /// <summary>Intervalo escolhido pelo usuário, dividido pelos dias de pagamento que caem dentro dele.</summary>
    private async Task<PrevisaoView> Intervalo(DateOnly de, DateOnly ate)
    {
        if (ate < de) throw new RegraNegocioException("A data final do período deve ser depois da inicial.");
        if (ate > de.AddYears(1)) throw new RegraNegocioException("Escolha um período de até um ano.");
        var pagamentos = await DiasDePagamento(de.AddMonths(-1), ate);
        var inicios = new List<DateOnly> { de };
        inicios.AddRange(pagamentos.Select(p => p.Data).Where(d => d > de && d <= ate).Distinct().Order());
        return await Montar(inicios, ate, pagamentos, porSalario: pagamentos.Count > 0, pularQuitados: false) with { Personalizado = true };
    }

    /// <summary>
    /// Monta os períodos a partir dos inícios. Com pularQuitados, o período de hoje sem nada pendente fica marcado
    /// como quitado e não conta na janela visível, que continua com <paramref name="visiveis"/> períodos.
    /// </summary>
    private async Task<PrevisaoView> Montar(List<DateOnly> inicios, DateOnly fim, List<DiaPagamento> pagamentos,
        bool porSalario, bool pularQuitados, int? visiveis = null)
    {
        await regras.Sincronizar(fim);
        var dados = await Carregar(inicios[0].AddMonths(-24), fim);

        var todos = new List<PeriodoPrevisao>();
        for (var i = 0; i < inicios.Count; i++)
        {
            var pIni = inicios[i];
            var pFim = i + 1 < inicios.Count ? inicios[i + 1].AddDays(-1) : fim;
            var nomes = pagamentos.Where(p => p.Data == pIni).Select(p => p.Nome).Distinct().ToList();
            var titulo = nomes.Count > 0 ? string.Join(" + ", nomes) : porSalario ? $"A partir de {pIni:dd/MM}" : "Mês";
            todos.Add(MontarPeriodo(dados, pIni, pFim, titulo));
        }

        var periodos = new List<PeriodoPrevisao>();
        var visiveisAte = visiveis ?? todos.Count;
        foreach (var p in todos)
        {
            var quitado = pularQuitados && periodos.Count == 0 && p.Inicio <= Hoje && !p.Itens.Any(i => i.Pendente);
            if (quitado) { periodos.Add(p with { Quitado = true }); continue; }
            if (periodos.Count(x => !x.Quitado) < visiveisAte) periodos.Add(p);
        }

        var abertos = periodos.Where(p => !p.Quitado).ToList();
        return new PrevisaoView(Hoje, abertos[0].Inicio, abertos[^1].Fim, porSalario, periodos,
            abertos[0].SaldoInicio, abertos[^1].SaldoFim,
            abertos.Sum(p => p.Entradas), abertos.Sum(p => p.Contas), abertos.Sum(p => p.Faturas), abertos.Sum(p => p.DiaADia));
    }

    /// <summary>
    /// Datas de pagamento (receitas recorrentes marcadas como salário) entre as datas. Usa a data real quando a
    /// ocorrência já existe e, antes do início da regra, a data que ela teria (só para marcar onde começa o ciclo).
    /// </summary>
    private async Task<List<DiaPagamento>> DiasDePagamento(DateOnly de, DateOnly ate)
    {
        var salarios = await RegrasDePagamento();
        if (salarios.Count == 0) return [];

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
                if (data >= de && data <= ate) dias.Add(new DiaPagamento(data, r.Descricao, r.Id));
            }
        }
        return dias.OrderBy(d => d.Data).ToList();
    }

    private async Task<List<Regra>> RegrasDePagamento()
    {
        var candidatas = await db.Regras.Include(r => r.Categoria).Include(r => r.Conta)
            .Where(r => r.Ativa && r.Modo == ModoRegra.Recorrente && r.Tipo == TipoLancamento.Receita)
            .ToListAsync();
        return candidatas.Where(r => r.Conta!.EhDisponivel && (r.IniciaPeriodo ?? EhSalario(r.Categoria!.Nome))).ToList();
    }

    public static bool EhSalario(string categoria) =>
        categoria.Trim().StartsWith("salário", StringComparison.OrdinalIgnoreCase)
        || categoria.Trim().StartsWith("salario", StringComparison.OrdinalIgnoreCase);

    private record Dados(
        decimal SaldoHoje,
        Func<DateOnly, decimal> SaldoReal,
        List<Lancamento> Lancamentos,
        List<(FaturaView Fatura, string? Cor)> Faturas,
        HashSet<Guid> Disponiveis,
        Dictionary<Guid, List<DiaPagamento>> Pagamentos);

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

        var lancamentos = await db.Lancamentos.Include(l => l.Categoria).Include(l => l.Conta).Include(l => l.Regra)
            .Where(l => l.Status != StatusLancamento.Cancelado && l.Tipo != TipoLancamento.Transferencia
                        && l.Data <= ate && (l.Data >= de || l.Status == StatusLancamento.Previsto))
            .OrderBy(l => l.Data).ThenBy(l => l.CriadoEm)
            .ToListAsync();
        lancamentos = lancamentos.Where(l => disponiveis.Contains(l.ContaId)).ToList();

        var faturas = new List<(FaturaView, string?)>();
        foreach (var c in contas.Where(c => c.EhCartao && c.Ativa))
            faturas.AddRange((await cartoes.Faturas(c.Id)).Where(f => f.Total > 0 || f.Pago > 0).Select(f => (f, c.Cor)));

        var pagamentos = (await DiasDePagamento(ate.AddMonths(-6), ate.AddMonths(2)))
            .GroupBy(p => p.RegraId).ToDictionary(g => g.Key, g => g.ToList());
        return new Dados(saldoHoje, SaldoReal, lancamentos, faturas, disponiveis, pagamentos);
    }

    /// <summary>Saldo do dinheiro disponível no fim do dia informado (real no passado, projetado do hoje em diante).</summary>
    private decimal SaldoEm(Dados d, DateOnly data)
    {
        if (data < Hoje) return d.SaldoReal(data);
        var pendentes = d.Lancamentos.Where(l => l.Status == StatusLancamento.Previsto && Pagavel(d, l).Data <= data)
            .Sum(l => l.Tipo == TipoLancamento.Receita ? l.Valor : -l.Valor);
        var faturas = d.Faturas.Where(f => f.Fatura.Restante > 0 && Calendario.ProximoDiaUtil(f.Fatura.Vencimento) <= data)
            .Sum(f => f.Fatura.Restante);
        return d.SaldoHoje + pendentes - faturas;
    }

    /// <summary>
    /// Data que decide com que dinheiro a conta é paga: o vencimento, ou o próximo dia útil quando vence em fim de
    /// semana/feriado (ex.: escola que vence sábado 07/11 é paga segunda 09/11, com o salário do 5º dia útil).
    /// Entradas e o que já foi pago ficam na data real.
    /// </summary>
    private static DateOnly PrazoSemJuros(Lancamento l) =>
        l.Tipo == TipoLancamento.Despesa && l.Status == StatusLancamento.Previsto ? Calendario.ProximoDiaUtil(l.Data) : l.Data;

    /// <summary>
    /// Em que dia (e com que pagamento) a conta entra na previsão. Conta marcada "pagar com" vai para a ocorrência
    /// desse pagamento mais próxima do prazo (ex.: escola 07/01 com o salário de 08/01). As demais, pelo prazo.
    /// </summary>
    private static (DateOnly Data, DiaPagamento? Pagamento) Pagavel(Dados d, Lancamento l)
    {
        var prazo = PrazoSemJuros(l);
        if (l.Status != StatusLancamento.Previsto || l.Tipo != TipoLancamento.Despesa
            || l.Regra?.PagoComRegraId is not { } id || !d.Pagamentos.TryGetValue(id, out var dias) || dias.Count == 0)
            return (prazo, null);
        var maisProximo = dias.OrderBy(p => Math.Abs(p.Data.DayNumber - prazo.DayNumber)).ThenBy(p => p.Data).First();
        return (maisProximo.Data, maisProximo);
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
            var prazo = PrazoSemJuros(l);
            var (pagavel, pagamento) = Pagavel(d, l);
            if (!Entra(pagavel, pendente)) continue;
            if (l.Tipo == TipoLancamento.Despesa && !l.Programado) continue;
            itens.Add(new ItemPrevisao(
                l.Tipo == TipoLancamento.Receita ? TipoItemPrevisao.Entrada : TipoItemPrevisao.Conta,
                l.Descricao, l.Categoria?.Nome, l.Categoria?.Cor, l.Categoria?.Icone, l.Data, l.Valor,
                pendente ? 0 : l.Valor, pendente, pendente && prazo < Hoje,
                l.Id, l.RegraId, null, null, l.Juros, prazo != l.Data ? prazo : null,
                pagamento?.Nome, pagamento?.Data,
                pagamento is not null && pagavel > prazo ? pagavel.DayNumber - prazo.DayNumber : null));
        }
        foreach (var (f, cor) in d.Faturas)
        {
            var pendente = f.Restante > 0;
            var pagavel = pendente ? Calendario.ProximoDiaUtil(f.Vencimento) : f.Vencimento;
            if (!Entra(pagavel, pendente)) continue;
            itens.Add(new ItemPrevisao(TipoItemPrevisao.Fatura, $"Fatura {f.Cartao}", StatusTexto(f.Status), cor, "mdi-credit-card-outline",
                f.Vencimento, f.Total, f.Pago, pendente, pendente && pagavel < Hoje, null, null, f.CartaoId, f.Mes, null,
                pagavel != f.Vencimento ? pagavel : null));
        }
        itens = itens.OrderBy(i => i.PagarEm ?? i.PagavelAte ?? i.Data).ThenBy(i => i.Tipo).ToList();

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
