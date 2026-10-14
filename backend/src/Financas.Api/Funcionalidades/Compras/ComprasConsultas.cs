using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Compras;

/// <summary>Leitura de uma compra importada pela nota, com os itens.</summary>
public class ComprasConsultas(AppDbContext db)
{
    public async Task<CompraView> Obter(Guid id)
    {
        var c = await db.Compras.Include(x => x.Itens).ThenInclude(i => i.Categoria).SingleOrDefaultAsync(x => x.Id == id)
                ?? throw new RegraNegocioException("Compra não encontrada.");
        var conta = await db.Contas.FindAsync(c.ContaId);
        return new CompraView(c.Id, c.Loja, c.Emissao, c.Total, c.Desconto, c.FormaPagamentoNota, c.ContaId, conta?.Nome ?? "",
            c.Itens.OrderBy(i => i.Sequencia).Select(i => new ItemCompraView(i.Sequencia, i.Descricao, i.Codigo, i.Quantidade, i.Unidade,
                i.ValorUnitario, i.ValorPago, i.CategoriaId, i.Categoria!.Nome, i.Categoria.Cor)).ToList());
    }
}
