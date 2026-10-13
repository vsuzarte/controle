using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Servicos;

public enum BaseSugestao { MesAnterior, Media3Meses }

public record CategoriaPlanejada(
    Guid CategoriaId,
    string Categoria,
    string? Cor,
    string? Icone,
    string? Grupo,
    decimal? Orcado,
    decimal? Sugerido,
    decimal Gasto,
    bool Editado,
    bool Dispensado,
    /// <summary>O que entra no resultado previsto: o maior entre orçado e já gasto (dispensado conta só o gasto).</summary>
    decimal Considerado);

public record PlanejamentoView(
    DateOnly Mes,
    List<LancamentoView> Receitas,
    List<LancamentoView> Compromissos,
    List<CategoriaPlanejada> Variaveis,
    decimal TotalReceitas,
    decimal ReceitasRecebidas,
    decimal TotalCompromissos,
    decimal CompromissosPagos,
    decimal TotalVariaveis,
    decimal GastoVariavel,
    decimal ResultadoPrevisto);

/// <summary>
/// Previsão do mês em três blocos que não se sobrepõem:
///   1. Receitas: todos os lançamentos de receita do mês (programados ou não).
///   2. Compromissos: despesas programadas (recorrências, parcelas, previstos avulsos). Protegidos:
///      só mudam no próprio lançamento ou na regra de origem.
///   3. Variáveis: orçamento por categoria, sugerido a partir do histórico de gastos NÃO programados.
/// Como cada despesa é programada ou variável (nunca as duas), nada é contado duas vezes.
/// </summary>
public class PlanejamentoService(AppDbContext db, IRelogio relogio, RegraService regras)
{
    public async Task<PlanejamentoView> Obter(DateOnly mes)
    {
        mes = Calendario.InicioMes(mes);
        var fim = Calendario.FimMes(mes);
        await regras.Sincronizar(fim);

        // Primeira visita a um mês atual/futuro sem orçamento: cria a sugestão automaticamente.
        if (mes >= Calendario.InicioMes(relogio.Hoje) && !await db.OrcamentoItens.AnyAsync(o => o.Mes == mes))
            await Sugerir(mes, BaseSugestao.MesAnterior);

        var lancs = await db.Lancamentos
            .Include(l => l.Conta).Include(l => l.Categoria).Include(l => l.Regra)
            .Where(l => l.Data >= mes && l.Data <= fim && l.Status != StatusLancamento.Cancelado
                        && l.Tipo != TipoLancamento.Transferencia)
            .OrderBy(l => l.Data)
            .ToListAsync();

        var receitas = lancs.Where(l => l.Tipo == TipoLancamento.Receita).ToList();
        var compromissos = lancs.Where(l => l.Tipo == TipoLancamento.Despesa && l.Programado).ToList();
        var gastosVariaveis = lancs
            .Where(l => l.Tipo == TipoLancamento.Despesa && !l.Programado && l.Status == StatusLancamento.Realizado)
            .GroupBy(l => l.CategoriaId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Valor));

        var itens = await db.OrcamentoItens.Where(o => o.Mes == mes).ToDictionaryAsync(o => o.CategoriaId);
        var idsCategorias = itens.Keys.Union(gastosVariaveis.Keys).ToList();
        var categorias = await db.Categorias.Where(c => idsCategorias.Contains(c.Id)).ToListAsync();

        var variaveis = categorias.Select(c =>
            {
                itens.TryGetValue(c.Id, out var item);
                var gasto = gastosVariaveis.GetValueOrDefault(c.Id);
                var dispensado = item?.Dispensado ?? false;
                var considerado = dispensado || item is null ? gasto : Math.Max(item.Valor, gasto);
                return new CategoriaPlanejada(c.Id, c.Nome, c.Cor, c.Icone, c.Grupo,
                    item?.Valor, item?.ValorSugerido, gasto, item?.Editado ?? false, dispensado, considerado);
            })
            .OrderBy(v => v.Dispensado).ThenByDescending(v => v.Considerado).ThenBy(v => v.Categoria)
            .ToList();

        var totalReceitas = receitas.Sum(l => l.Valor);
        var totalCompromissos = compromissos.Sum(l => l.Valor);
        var totalVariaveis = variaveis.Sum(v => v.Considerado);

        return new PlanejamentoView(
            mes,
            receitas.Select(LancamentoView.De).ToList(),
            compromissos.Select(LancamentoView.De).ToList(),
            variaveis,
            totalReceitas,
            receitas.Where(l => l.Status == StatusLancamento.Realizado).Sum(l => l.Valor),
            totalCompromissos,
            compromissos.Where(l => l.Status == StatusLancamento.Realizado).Sum(l => l.Valor),
            totalVariaveis,
            gastosVariaveis.Values.Sum(),
            totalReceitas - totalCompromissos - totalVariaveis);
    }

    /// <summary>
    /// Cria itens de orçamento para categorias que ainda não têm item no mês. Nunca altera itens
    /// existentes (editados, mantidos ou dispensados), então rodar várias vezes não duplica nem desfaz decisões.
    /// </summary>
    public async Task<int> Sugerir(DateOnly mes, BaseSugestao baseSugestao)
    {
        mes = Calendario.InicioMes(mes);
        var meses = baseSugestao == BaseSugestao.Media3Meses ? 3 : 1;
        var inicio = mes.AddMonths(-meses);
        var fim = mes.AddDays(-1);

        var historico = await db.Lancamentos
            .Where(l => l.Tipo == TipoLancamento.Despesa && !l.Programado && l.Status == StatusLancamento.Realizado
                        && l.Data >= inicio && l.Data <= fim && l.CategoriaId != null)
            .Select(l => new { CategoriaId = l.CategoriaId!.Value, l.Valor })
            .ToListAsync();

        var porCategoria = historico.GroupBy(h => h.CategoriaId)
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(h => h.Valor) / meses, 2));

        var ativas = await db.Categorias.Where(c => c.Ativa && c.Tipo == TipoCategoria.Despesa).Select(c => c.Id).ToListAsync();
        var jaExistem = await db.OrcamentoItens.Where(o => o.Mes == mes).Select(o => o.CategoriaId).ToListAsync();

        var criados = 0;
        foreach (var (categoriaId, valor) in porCategoria)
        {
            if (valor <= 0 || !ativas.Contains(categoriaId) || jaExistem.Contains(categoriaId)) continue;
            db.OrcamentoItens.Add(new OrcamentoItem { Mes = mes, CategoriaId = categoriaId, Valor = valor, ValorSugerido = valor });
            criados++;
        }
        if (criados > 0)
        {
            db.DefinirAuditoria("Sugestão de orçamento",
                baseSugestao == BaseSugestao.MesAnterior ? "Base: mês anterior" : "Base: média de 3 meses");
            await db.SaveChangesAsync();
        }
        return criados;
    }

    public async Task<OrcamentoItem> DefinirValor(DateOnly mes, Guid categoriaId, ValorOrcamentoDto dto)
    {
        var item = await ObterOuCriar(mes, categoriaId);
        var valor = decimal.Round(dto.Valor, 2);
        if (valor < 0) throw new RegraNegocioException("O valor orçado não pode ser negativo.");
        item.Valor = valor;
        item.Editado = true;
        item.Dispensado = false;
        db.DefinirAuditoria("Orçamento editado", dto.Motivo);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<OrcamentoItem> Dispensar(DateOnly mes, Guid categoriaId, string? motivo)
    {
        var item = await ObterOuCriar(mes, categoriaId);
        item.Dispensado = true;
        db.DefinirAuditoria("Removido da previsão", motivo);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<OrcamentoItem> Restaurar(DateOnly mes, Guid categoriaId)
    {
        var item = await db.OrcamentoItens.SingleOrDefaultAsync(o => o.Mes == Calendario.InicioMes(mes) && o.CategoriaId == categoriaId)
                   ?? throw new RegraNegocioException("Item de orçamento não encontrado.");
        item.Dispensado = false;
        db.DefinirAuditoria("Restaurado na previsão");
        await db.SaveChangesAsync();
        return item;
    }

    private async Task<OrcamentoItem> ObterOuCriar(DateOnly mes, Guid categoriaId)
    {
        mes = Calendario.InicioMes(mes);
        var item = await db.OrcamentoItens.SingleOrDefaultAsync(o => o.Mes == mes && o.CategoriaId == categoriaId);
        if (item is not null) return item;

        var categoria = await db.Categorias.FindAsync(categoriaId) ?? throw new RegraNegocioException("Categoria não encontrada.");
        if (categoria.Tipo != TipoCategoria.Despesa) throw new RegraNegocioException("Orçamento é só para categorias de despesa.");
        item = new OrcamentoItem { Mes = mes, CategoriaId = categoriaId };
        db.OrcamentoItens.Add(item);
        return item;
    }
}
