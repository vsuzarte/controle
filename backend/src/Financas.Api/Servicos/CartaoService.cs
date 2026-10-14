using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Servicos;

public enum StatusFatura { Aberta, Fechada, Paga, Vencida, Futura }

public record FaturaView(
    Guid CartaoId,
    string Cartao,
    DateOnly Mes,
    DateOnly Fechamento,
    DateOnly Vencimento,
    decimal Total,
    decimal Pago,
    decimal Restante,
    StatusFatura Status,
    List<LancamentoView> Lancamentos,
    List<LancamentoView> Pagamentos);

public record CartaoResumo(Guid Id, string Nome, string? Cor, int DiaFechamento, int DiaVencimento,
    decimal? Limite, decimal LimiteUsado, decimal? LimiteDisponivel, List<FaturaView> APagar, FaturaView? Aberta);

public record TodosCartoesView(List<CartaoResumo> Cartoes, decimal TotalAPagar, decimal TotalEmAberto, decimal LimiteTotal, decimal LimiteUsadoTotal);

/// <summary>
/// A fatura não é uma tabela: é a soma dos lançamentos do cartão com aquele FaturaMes.
/// Pagar a fatura é uma transferência da conta bancária para o cartão. O gasto já foi
/// contado por categoria na compra, então o pagamento não é despesa (sem contar duas vezes).
/// </summary>
public class CartaoService(AppDbContext db, IRelogio relogio)
{
    public async Task<FaturaView> Fatura(Guid cartaoId, DateOnly mes)
    {
        var cartao = await Cartao(cartaoId);
        mes = Calendario.InicioMes(mes);
        var itens = await db.Lancamentos
            .Include(l => l.Conta).Include(l => l.Categoria).Include(l => l.Regra).Include(l => l.ContaDestino)
            .Where(l => l.FaturaMes == mes && (l.ContaId == cartaoId || l.ContaDestinoId == cartaoId))
            .OrderBy(l => l.Data).ThenBy(l => l.CriadoEm)
            .ToListAsync();
        return Montar(cartao, mes, itens);
    }

    /// <summary>Faturas com movimento ou ainda não pagas, da mais recente para a mais antiga.</summary>
    public async Task<List<FaturaView>> Faturas(Guid cartaoId)
    {
        var cartao = await Cartao(cartaoId);
        var itens = await db.Lancamentos
            .Include(l => l.Conta).Include(l => l.Categoria).Include(l => l.Regra).Include(l => l.ContaDestino)
            .Where(l => l.FaturaMes != null && (l.ContaId == cartaoId || l.ContaDestinoId == cartaoId))
            .ToListAsync();
        var atual = Calendario.FaturaDaCompra(cartao, relogio.Hoje);
        return itens.Select(l => l.FaturaMes!.Value).Append(atual).Distinct()
            .OrderByDescending(m => m)
            .Select(m => Montar(cartao, m, itens.Where(l => l.FaturaMes == m).OrderBy(l => l.Data).ToList()))
            .ToList();
    }

    public async Task<Lancamento> Pagar(Guid cartaoId, DateOnly mes, PagarFaturaDto dto)
    {
        var cartao = await Cartao(cartaoId);
        var origem = await db.Contas.FindAsync(dto.ContaOrigemId) ?? throw new RegraNegocioException("Conta de origem não encontrada.");
        if (origem.EhCartao) throw new RegraNegocioException("A fatura deve ser paga a partir de uma conta, não de outro cartão.");
        if (origem.EhBeneficio) throw new RegraNegocioException("A fatura não pode ser paga com o vale.");
        if (!origem.Ativa) throw new RegraNegocioException($"A conta '{origem.Nome}' está desativada.");
        if (dto.Data > relogio.Hoje) throw new RegraNegocioException("A data do pagamento não pode ser futura.");

        mes = Calendario.InicioMes(mes);
        var valor = Validar.ValorPositivo(dto.Valor);
        var juros = Validar.Juros(dto.Juros, valor);
        var pagamento = new Lancamento
        {
            Tipo = TipoLancamento.Transferencia,
            Status = StatusLancamento.Realizado,
            Descricao = $"Pagamento fatura {cartao.Nome} {mes:MM/yyyy}",
            Valor = valor - (juros ?? 0),
            Data = dto.Data,
            ContaId = origem.Id,
            ContaDestinoId = cartao.Id,
            FormaPagamento = FormaPagamento.Transferencia,
            FaturaMes = mes,
        };
        db.Lancamentos.Add(pagamento);
        if (juros is { } j)
        {
            // Juros/multa não abatem a fatura: são dinheiro perdido, lançado como despesa à parte.
            db.Lancamentos.Add(new Lancamento
            {
                Tipo = TipoLancamento.Despesa,
                Status = StatusLancamento.Realizado,
                Descricao = $"Juros/multa fatura {cartao.Nome} {mes:MM/yyyy}",
                Valor = j,
                Juros = j,
                Data = dto.Data,
                ContaId = origem.Id,
                CategoriaId = (await CategoriaJuros()).Id,
                FormaPagamento = FormaPagamento.Transferencia,
                Programado = true,
            });
        }
        db.DefinirAuditoria("Pagamento de fatura");
        await db.SaveChangesAsync();
        return pagamento;
    }

    /// <summary>
    /// Quanto do limite de cada cartão está comprometido, como o banco calcula: compras ainda não pagas,
    /// contando o total das parcelas futuras (a compra parcelada ocupa o limite inteiro na hora),
    /// menos estornos e pagamentos de fatura já feitos. Cobranças recorrentes futuras não ocupam limite.
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> LimitesUsados()
    {
        var cartoes = await db.Contas.Where(c => c.Tipo == TipoConta.CartaoCredito).Select(c => c.Id).ToListAsync();
        var movimentos = await db.Lancamentos
            .Where(l => l.Status != StatusLancamento.Cancelado
                        && (cartoes.Contains(l.ContaId) || (l.ContaDestinoId != null && cartoes.Contains(l.ContaDestinoId.Value))))
            .Select(l => new { l.ContaId, l.ContaDestinoId, l.Tipo, l.Status, l.Valor, Parcela = l.Regra != null && l.Regra.Modo == ModoRegra.Parcelado })
            .ToListAsync();

        var usado = cartoes.ToDictionary(id => id, _ => 0m);
        foreach (var m in movimentos)
        {
            if (m.Tipo == TipoLancamento.Transferencia)
            {
                // Pagamento de fatura libera limite (só o que já foi pago de fato).
                if (m.Status == StatusLancamento.Realizado && m.ContaDestinoId is { } destino && usado.ContainsKey(destino))
                    usado[destino] -= m.Valor;
                continue;
            }
            if (m.Status == StatusLancamento.Previsto && !m.Parcela) continue;
            usado[m.ContaId] += m.Tipo == TipoLancamento.Despesa ? m.Valor : -m.Valor;
        }
        return usado.ToDictionary(kv => kv.Key, kv => Math.Max(0, kv.Value));
    }

    /// <summary>Visão de todos os cartões: fatura a pagar, fatura em aberto e limite de cada um, com os totais.</summary>
    public async Task<TodosCartoesView> Todos()
    {
        var cartoes = await db.Contas.Where(c => c.Tipo == TipoConta.CartaoCredito && c.Ativa).OrderBy(c => c.Nome).ToListAsync();
        var usados = await LimitesUsados();
        var lista = new List<CartaoResumo>();
        foreach (var c in cartoes)
        {
            var faturas = await Faturas(c.Id);
            var aPagar = faturas.Where(f => f.Restante > 0 && f.Status is StatusFatura.Fechada or StatusFatura.Vencida)
                .OrderBy(f => f.Vencimento).Select(f => f with { Lancamentos = [], Pagamentos = [] }).ToList();
            var aberta = faturas.FirstOrDefault(f => f.Status == StatusFatura.Aberta);
            var usado = usados.GetValueOrDefault(c.Id);
            lista.Add(new CartaoResumo(c.Id, c.Nome, c.Cor, c.DiaFechamento!.Value, c.DiaVencimento!.Value, c.Limite, usado,
                c.Limite is { } l ? l - usado : null, aPagar, aberta is null ? null : aberta with { Lancamentos = [], Pagamentos = [] }));
        }
        return new TodosCartoesView(lista,
            lista.Sum(c => c.APagar.Sum(f => f.Restante)),
            lista.Sum(c => c.Aberta?.Restante ?? 0),
            lista.Where(c => c.Limite is not null).Sum(c => c.Limite!.Value),
            lista.Where(c => c.Limite is not null).Sum(c => c.LimiteUsado));
    }

    private async Task<Categoria> CategoriaJuros()
    {
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Tipo == TipoCategoria.Despesa
                            && (c.Nome == "Tarifas e juros" || c.Nome == "Juros e tarifas"));
        if (categoria is not null) return categoria;
        categoria = new Categoria { Nome = "Tarifas e juros", Tipo = TipoCategoria.Despesa, Grupo = "Financeiras", Icone = "mdi-bank", Cor = "#757575" };
        db.Categorias.Add(categoria);
        return categoria;
    }

    private FaturaView Montar(Conta cartao, DateOnly mes, List<Lancamento> itens)
    {
        var validos = itens.Where(l => l.Valido).ToList();
        var compras = validos.Where(l => l.ContaId == cartao.Id && l.Tipo != TipoLancamento.Transferencia).ToList();
        var pagamentos = validos.Where(l => l.Tipo == TipoLancamento.Transferencia && l.ContaDestinoId == cartao.Id).ToList();

        var total = compras.Sum(l => l.Tipo == TipoLancamento.Despesa ? l.Valor : -l.Valor);
        var pago = pagamentos.Where(p => p.Status == StatusLancamento.Realizado).Sum(p => p.Valor);
        var restante = Math.Max(0, total - pago);
        var fechamento = Calendario.FechamentoDaFatura(cartao, mes);
        var vencimento = Calendario.VencimentoDaFatura(cartao, mes);
        var hoje = relogio.Hoje;

        var status = total > 0 && restante == 0 ? StatusFatura.Paga
            : vencimento < hoje && restante > 0 ? StatusFatura.Vencida
            : hoje >= fechamento ? StatusFatura.Fechada
            : Calendario.FaturaDaCompra(cartao, hoje) == mes ? StatusFatura.Aberta
            : StatusFatura.Futura;

        return new FaturaView(cartao.Id, cartao.Nome, mes, fechamento, vencimento, total, pago, restante, status,
            itens.Where(l => l.ContaId == cartao.Id && l.Tipo != TipoLancamento.Transferencia).Select(LancamentoView.De).ToList(),
            itens.Where(l => l.Tipo == TipoLancamento.Transferencia).Select(LancamentoView.De).ToList());
    }

    private async Task<Conta> Cartao(Guid id)
    {
        var c = await db.Contas.FindAsync(id) ?? throw new RegraNegocioException("Cartão não encontrado.");
        return c.EhCartao ? c : throw new RegraNegocioException($"'{c.Nome}' não é um cartão de crédito.");
    }
}
