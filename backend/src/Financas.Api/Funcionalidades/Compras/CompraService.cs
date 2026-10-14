using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Lancamentos;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Compras;

/// <summary>
/// Compras pela nota fiscal: lê a nota para conferir e importa. A importação guarda a compra com os itens
/// e cria um lançamento por categoria (o total bate com a cobrança no banco/cartão).
/// </summary>
public class CompraService(AppDbContext db, LeitorNfce leitor, LancamentoService lancamentos)
{
    public async Task<LeituraNotaView> Ler(string? url, CancellationToken ct = default)
    {
        var endereco = LeitorNfce.Validar(url).ToString();
        var nota = await leitor.Buscar(endereco, ct);
        var existente = await db.Compras.Where(c => c.ChaveAcesso == nota.Chave).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);

        var padrao = await CategoriaPadrao();
        var historico = await CategoriasUsadas(nota.Itens);
        var pagos = Ratear(nota.Itens.Select(i => i.ValorTotal).ToList(), nota.ValorAPagar);
        var itens = nota.Itens.Select((i, n) => new ItemLidoView(i.Sequencia, i.Descricao, i.Codigo, i.Quantidade, i.Unidade,
                i.ValorUnitario, i.ValorTotal, pagos[n], CategoriaSugerida(i, historico) ?? padrao?.Id ?? Guid.Empty))
            .ToList();

        var forma = nota.Pagamentos.Count > 0 ? string.Join(" + ", nota.Pagamentos.Select(p => p.Forma)) : null;
        var apelido = nota.Cnpj is null ? null
            : await db.Compras.Where(c => c.Cnpj == nota.Cnpj).OrderByDescending(c => c.CriadoEm).Select(c => c.Loja).FirstOrDefaultAsync(ct);

        return new LeituraNotaView(endereco, nota.Chave, apelido ?? nota.Loja, nota.Loja, nota.Cnpj, DateOnly.FromDateTime(nota.Emissao),
            nota.Emissao, nota.ValorTotal, nota.Desconto, nota.ValorAPagar, forma, await ContaSugerida(nota, ct), padrao?.Id, itens, existente);
    }

    public async Task<Compra> Importar(ImportarCompraDto dto, CancellationToken ct = default)
    {
        var nota = await leitor.Buscar(LeitorNfce.Validar(dto.Url).ToString(), ct);
        if (await db.Compras.AnyAsync(c => c.ChaveAcesso == nota.Chave, ct))
            throw new RegraNegocioException("Esta nota já foi lançada.");
        var conta = await db.Contas.FindAsync([dto.ContaId], ct) ?? throw new RegraNegocioException("Escolha com que conta pagou.");

        var categorias = dto.Itens.ToDictionary(i => i.Sequencia, i => i.CategoriaId);
        var padrao = (await CategoriaPadrao())?.Id;
        var pagos = Ratear(nota.Itens.Select(i => i.ValorTotal).ToList(), nota.ValorAPagar);

        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        var compra = new Compra
        {
            ChaveAcesso = nota.Chave,
            Loja = Validar.Opcional(dto.Loja) ?? nota.Loja,
            Cnpj = nota.Cnpj,
            Emissao = nota.Emissao,
            Total = nota.ValorAPagar,
            Desconto = nota.Desconto,
            FormaPagamentoNota = nota.Pagamentos.Count > 0 ? string.Join(" + ", nota.Pagamentos.Select(p => p.Forma)) : null,
            ContaId = conta.Id,
            Itens = nota.Itens.Select((i, n) => new ItemCompra
            {
                Sequencia = i.Sequencia,
                Descricao = i.Descricao,
                Codigo = i.Codigo,
                Quantidade = i.Quantidade,
                Unidade = i.Unidade,
                ValorUnitario = i.ValorUnitario,
                ValorTotal = i.ValorTotal,
                ValorPago = pagos[n],
                CategoriaId = categorias.GetValueOrDefault(i.Sequencia, padrao ?? Guid.Empty),
            }).ToList(),
        };
        if (compra.Itens.Any(i => i.CategoriaId == Guid.Empty)) throw new RegraNegocioException("Escolha a categoria de todos os itens.");
        db.Compras.Add(compra);
        db.DefinirAuditoria("Compra pela nota fiscal");
        await db.SaveChangesAsync(ct);

        // Um lançamento por categoria: o total da nota continua uma cobrança só no banco/cartão.
        var forma = FormaPagamentoDaNota(nota);
        foreach (var grupo in compra.Itens.GroupBy(i => i.CategoriaId))
        {
            var valor = grupo.Sum(i => i.ValorPago);
            if (valor <= 0) continue;
            var criados = await lancamentos.Criar(new NovoLancamentoDto(TipoLancamento.Despesa, compra.Loja, valor,
                DateOnly.FromDateTime(nota.Emissao), conta.Id, null, grupo.Key, forma,
                $"Nota fiscal: {grupo.Count()} item(ns)", null, null, null));
            foreach (var l in criados) l.CompraId = compra.Id;
            foreach (var item in grupo) item.LancamentoId = criados[0].Id;
        }
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return compra;
    }

    /// <summary>
    /// Divide o desconto da nota entre os itens, proporcional ao valor de cada um. A diferença de centavos vai
    /// no maior item, para a soma bater exatamente com o valor pago.
    /// </summary>
    public static List<decimal> Ratear(List<decimal> valores, decimal totalPago)
    {
        var soma = valores.Sum();
        if (soma <= 0) return valores.ToList();
        var pagos = valores.Select(v => decimal.Round(v * totalPago / soma, 2, MidpointRounding.AwayFromZero)).ToList();
        var maior = valores.IndexOf(valores.Max());
        pagos[maior] += totalPago - pagos.Sum();
        return pagos;
    }

    private static FormaPagamento? FormaPagamentoDaNota(NotaLida nota)
    {
        var forma = nota.Pagamentos.FirstOrDefault()?.Forma.ToLowerInvariant() ?? "";
        return forma switch
        {
            _ when forma.Contains("crédito") || forma.Contains("credito") => FormaPagamento.CartaoCredito,
            _ when forma.Contains("débito") || forma.Contains("debito") => FormaPagamento.CartaoDebito,
            _ when forma.Contains("pix") => FormaPagamento.Pix,
            _ when forma.Contains("dinheiro") => FormaPagamento.Dinheiro,
            _ => FormaPagamento.Outro,
        };
    }

    /// <summary>Conta que provavelmente pagou: crédito → cartão; vale → vale; dinheiro → carteira; o resto → banco.</summary>
    private async Task<Guid?> ContaSugerida(NotaLida nota, CancellationToken ct)
    {
        var forma = nota.Pagamentos.FirstOrDefault()?.Forma.ToLowerInvariant() ?? "";
        var contas = await db.Contas.Where(c => c.Ativa).OrderBy(c => c.Nome).ToListAsync(ct);
        Conta? Primeira(Func<Conta, bool> f) => contas.FirstOrDefault(f);
        return (forma switch
        {
            _ when forma.Contains("vale") || forma.Contains("alimenta") || forma.Contains("refei") => Primeira(c => c.EhBeneficio),
            _ when forma.Contains("crédito") || forma.Contains("credito") => Primeira(c => c.EhCartao),
            _ when forma.Contains("dinheiro") => Primeira(c => c.Tipo == TipoConta.Dinheiro),
            _ => null,
        } ?? Primeira(c => c.Tipo == TipoConta.Corrente) ?? Primeira(c => c.EhDisponivel))?.Id;
    }

    private async Task<Categoria?> CategoriaPadrao()
    {
        var despesas = await db.Categorias.Where(c => c.Ativa && c.Tipo == TipoCategoria.Despesa).ToListAsync();
        return despesas.FirstOrDefault(c => c.Nome.StartsWith("Mercado", StringComparison.OrdinalIgnoreCase)) ?? despesas.OrderBy(c => c.Nome).FirstOrDefault();
    }

    /// <summary>Última categoria usada para o mesmo produto (pelo código ou pelo nome): a escolha de antes vira a sugestão.</summary>
    private async Task<List<(string? Codigo, string Descricao, Guid CategoriaId)>> CategoriasUsadas(List<ItemNota> itens)
    {
        var codigos = itens.Where(i => i.Codigo != null).Select(i => i.Codigo).ToList();
        var nomes = itens.Select(i => i.Descricao).ToList();
        var anteriores = await db.ItensCompra
            .Where(i => (i.Codigo != null && codigos.Contains(i.Codigo)) || nomes.Contains(i.Descricao))
            .OrderByDescending(i => i.Id)
            .Select(i => new { i.Codigo, i.Descricao, i.CategoriaId })
            .ToListAsync();
        return anteriores.Select(a => (a.Codigo, a.Descricao, a.CategoriaId)).ToList();
    }

    private static Guid? CategoriaSugerida(ItemNota item, List<(string? Codigo, string Descricao, Guid CategoriaId)> historico)
    {
        // Códigos curtos (ex.: "2769") são internos da loja; só o código de barras (8+ dígitos) vale entre lojas.
        var porCodigo = item.Codigo is { Length: >= 8 } ? historico.FirstOrDefault(h => h.Codigo == item.Codigo) : default;
        if (porCodigo != default) return porCodigo.CategoriaId;
        var porNome = historico.FirstOrDefault(h => h.Descricao == item.Descricao);
        return porNome != default ? porNome.CategoriaId : null;
    }
}
