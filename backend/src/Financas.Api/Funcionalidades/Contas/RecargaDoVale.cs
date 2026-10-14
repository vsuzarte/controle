using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Regras;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>
/// A recarga do vale é uma entrada recorrente na conta do vale (como uma conta do mês), que se confirma
/// sozinha na data. Aqui ela é cadastrada direto pelo vale, sem precisar passar por Contas do mês.
/// </summary>
public class RecargaDoVale(AppDbContext db, IRelogio relogio, RegraService regras)
{
    public async Task<RecargaView?> Definir(Guid contaId, RecargaDto dto)
    {
        var vale = await db.Contas.FindAsync(contaId) ?? throw new RegraNegocioException("Conta não encontrada.");
        if (!vale.EhBeneficio) throw new RegraNegocioException("Recarga automática é só para vale-refeição/alimentação.");
        var atual = await Regra(vale.Id);

        if (dto.Valor is null or 0)
        {
            if (atual is not null) await regras.Desativar(atual.Id, new DesativarRegraDto(true, "Recarga do vale removida"));
            return null;
        }

        var dia = dto.Vencimento == RegraVencimento.DiaFixo ? dto.Dia ?? throw new RegraNegocioException("Informe o dia da recarga.") : (int?)null;
        if (atual is null)
        {
            var categoria = await CategoriaBeneficios();
            await regras.Criar(new RegraDto(ModoRegra.Recorrente, TipoLancamento.Receita, $"Recarga {vale.Nome}", categoria.Id, vale.Id,
                null, dto.Valor.Value, false, null, Frequencia.Mensal, 1, PrimeiraData(dia), null, null,
                dto.Vencimento, dto.DiaUtilNumero, IniciaPeriodo: false));
        }
        else
        {
            await regras.Editar(atual.Id, new EditarRegraDto(atual.Descricao, atual.CategoriaId, atual.ContaId, atual.FormaPagamento,
                dto.Valor.Value, false, atual.DataFim, atual.Observacao, AplicarAosPrevistos: true, Motivo: "Recarga do vale alterada",
                dto.Vencimento, dto.DiaUtilNumero, dia, IniciaPeriodo: false));
        }
        return (await Todas()).GetValueOrDefault(vale.Id);
    }

    /// <summary>Recarga de cada vale que tem uma.</summary>
    public async Task<Dictionary<Guid, RecargaView>> Todas()
    {
        var hoje = relogio.Hoje;
        var vales = await db.Contas.Where(c => c.Tipo == TipoConta.Beneficio).Select(c => c.Id).ToListAsync();
        if (vales.Count == 0) return [];
        var lista = await db.Regras
            .Where(r => r.Ativa && r.Modo == ModoRegra.Recorrente && r.Tipo == TipoLancamento.Receita && vales.Contains(r.ContaId))
            .OrderBy(r => r.CriadoEm).ToListAsync();
        var ids = lista.Select(r => r.Id).ToList();
        var proximas = (await db.Lancamentos
                .Where(l => l.RegraId != null && ids.Contains(l.RegraId.Value) && l.Status == StatusLancamento.Previsto && l.Data >= hoje)
                .Select(l => new { RegraId = l.RegraId!.Value, l.Data })
                .ToListAsync())
            .GroupBy(l => l.RegraId)
            .ToDictionary(g => g.Key, g => g.Min(l => l.Data));

        var resultado = new Dictionary<Guid, RecargaView>();
        foreach (var r in lista.Where(r => !resultado.ContainsKey(r.ContaId)))
            resultado[r.ContaId] = new RecargaView(r.Id, r.Valor, r.Vencimento ?? RegraVencimento.DiaFixo, r.DiaUtilNumero,
                r.DataInicio.Day, proximas.TryGetValue(r.Id, out var d) ? d : null);
        return resultado;
    }

    private Task<Regra?> Regra(Guid valeId) =>
        db.Regras.Where(r => r.Ativa && r.Modo == ModoRegra.Recorrente && r.Tipo == TipoLancamento.Receita && r.ContaId == valeId)
            .OrderBy(r => r.CriadoEm).FirstOrDefaultAsync();

    /// <summary>Dia fixo: neste mês se ainda não passou, senão no próximo. Dia útil/último dia: a regra acha a primeira data a partir de hoje.</summary>
    private DateOnly PrimeiraData(int? dia)
    {
        var hoje = relogio.Hoje;
        if (dia is not { } d) return hoje;
        if (d is < 1 or > 31) throw new RegraNegocioException("O dia da recarga deve ser de 1 a 31.");
        var nesteMes = Calendario.DiaNoMes(hoje, d);
        return nesteMes >= hoje ? nesteMes : Calendario.DiaNoMes(hoje.AddMonths(1), d);
    }

    private async Task<Categoria> CategoriaBeneficios()
    {
        var existentes = await db.Categorias.Where(c => c.Tipo == TipoCategoria.Receita).ToListAsync();
        var categoria = existentes.FirstOrDefault(c => c.Nome.StartsWith("Benef", StringComparison.OrdinalIgnoreCase)
                                                       || c.Nome.StartsWith("Vale", StringComparison.OrdinalIgnoreCase));
        if (categoria is not null) return categoria;
        categoria = new Categoria { Nome = "Benefícios", Tipo = TipoCategoria.Receita, Grupo = "Entradas", Icone = "mdi-food", Cor = "#F59E0B" };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync();
        return categoria;
    }
}
