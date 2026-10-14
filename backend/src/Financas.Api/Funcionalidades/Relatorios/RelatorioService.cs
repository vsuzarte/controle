using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Lancamentos;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Relatorios;

/// <summary>
/// Comparativo de gastos mês a mês, para ver onde as coisas estão aumentando.
///   - Categorias: despesas pelo mês em que aconteceram (compra no cartão conta no dia da compra).
///   - Contas fixas: cada recorrência (água, luz, internet...) pelo valor efetivamente pago.
///   - Cartões: total de cada fatura, pelo mês de vencimento.
///   - Juros: o que foi pago a mais por atraso (informado no pagamento) e a categoria "Tarifas e juros".
/// Só conta o que foi realizado; estornos abatem.
/// </summary>
public class RelatorioService(AppDbContext db)
{
    public async Task<ComparativoView> Comparativo(DateOnly ate, int quantidade)
    {
        quantidade = Math.Clamp(quantidade, 2, 24);
        var ultimo = Calendario.InicioMes(ate);
        var meses = Enumerable.Range(0, quantidade).Select(i => ultimo.AddMonths(i - quantidade + 1)).ToList();
        var de = meses[0];
        var fim = Calendario.FimMes(ultimo);
        int Indice(DateOnly d) => meses.IndexOf(Calendario.InicioMes(d));

        var despesas = await db.Lancamentos
            .Include(l => l.Categoria).Include(l => l.Conta).Include(l => l.Regra)
            .Where(l => l.Tipo == TipoLancamento.Despesa && l.Status == StatusLancamento.Realizado && l.Data >= de && l.Data <= fim)
            .ToListAsync();

        List<decimal> Vazio() => Enumerable.Repeat(0m, quantidade).ToList();

        var total = Vazio();
        foreach (var l in despesas) total[Indice(l.Data)] += l.Valor;

        var categorias = despesas.Where(l => l.Categoria is not null).GroupBy(l => l.CategoriaId!.Value)
            .Select(g =>
            {
                var v = Vazio();
                foreach (var l in g) v[Indice(l.Data)] += l.Valor;
                var c = g.First().Categoria!;
                return new SerieMensal(c.Id, c.Nome, c.Cor, c.Icone, v);
            })
            .OrderByDescending(s => s.Valores[^1]).ThenByDescending(s => s.Valores.Sum()).ToList();

        var fixas = despesas.Where(l => l.Regra is { Modo: ModoRegra.Recorrente }).GroupBy(l => l.RegraId!.Value)
            .Select(g =>
            {
                var v = Vazio();
                foreach (var l in g) v[Indice(l.Data)] += l.Valor;
                var r = g.First();
                return new SerieMensal(r.RegraId, r.Regra!.Descricao, r.Categoria?.Cor, r.Categoria?.Icone, v);
            })
            .OrderBy(s => s.Nome).ToList();

        // Faturas pelo mês de vencimento (inclui as compras ainda previstas da fatura do último mês).
        var cartoes = await db.Contas.Where(c => c.Tipo == TipoConta.CartaoCredito).ToListAsync();
        var idsCartoes = cartoes.Select(c => c.Id).ToList();
        var compras = await db.Lancamentos
            .Where(l => idsCartoes.Contains(l.ContaId) && l.Tipo != TipoLancamento.Transferencia && l.Status != StatusLancamento.Cancelado
                        && l.FaturaMes >= de && l.FaturaMes <= ultimo)
            .Select(l => new { l.ContaId, l.Tipo, l.Valor, l.FaturaMes })
            .ToListAsync();
        var seriesCartoes = cartoes
            .Select(c =>
            {
                var v = Vazio();
                foreach (var l in compras.Where(x => x.ContaId == c.Id))
                    v[Indice(l.FaturaMes!.Value)] += l.Tipo == TipoLancamento.Despesa ? l.Valor : -l.Valor;
                return new SerieMensal(c.Id, c.Nome, c.Cor, "mdi-credit-card-outline", v);
            })
            .Where(s => s.Valores.Any(x => x != 0) || cartoes.First(c => c.Id == s.Id).Ativa)
            .ToList();

        var juros = Vazio();
        var jurosDetalhe = new List<Lancamento>();
        foreach (var l in despesas)
        {
            var perdido = l.Juros ?? (EhCategoriaJuros(l.Categoria) ? l.Valor : 0);
            if (perdido == 0) continue;
            juros[Indice(l.Data)] += perdido;
            jurosDetalhe.Add(l);
        }

        return new ComparativoView(meses, new SerieMensal(null, "Total", null, null, total), categorias, fixas, seriesCartoes,
            new SerieMensal(null, "Juros e multas", null, "mdi-alert-circle-outline", juros),
            jurosDetalhe.OrderByDescending(l => l.Data).Select(LancamentoView.De).ToList());
    }

    private static bool EhCategoriaJuros(Categoria? c) =>
        c is not null && (c.Nome.Contains("juros", StringComparison.OrdinalIgnoreCase) || c.Nome.Contains("tarifa", StringComparison.OrdinalIgnoreCase));
}
