using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Dados;

/// <summary>Cadastros iniciais criados apenas no primeiro uso. Tudo pode ser editado ou desativado depois.</summary>
public static class DadosIniciais
{
    public static async Task Aplicar(AppDbContext db)
    {
        if (await db.Contas.AnyAsync() || await db.Categorias.AnyAsync()) return;

        db.Contas.AddRange(
            new Conta { Nome = "Conta corrente", Tipo = TipoConta.Corrente, Cor = "#1E88E5" },
            new Conta { Nome = "Dinheiro", Tipo = TipoConta.Dinheiro, Cor = "#43A047" });

        Categoria C(string nome, TipoCategoria tipo, string grupo, string icone, string cor) =>
            new() { Nome = nome, Tipo = tipo, Grupo = grupo, Icone = icone, Cor = cor };

        const TipoCategoria R = TipoCategoria.Receita, D = TipoCategoria.Despesa;
        db.Categorias.AddRange(
            C("Salário", R, "Entradas", "mdi-briefcase", "#2E7D32"),
            C("Férias / 13º", R, "Entradas", "mdi-beach", "#388E3C"),
            C("Renda extra", R, "Entradas", "mdi-cash-plus", "#43A047"),
            C("Reembolso", R, "Entradas", "mdi-cash-refund", "#66BB6A"),
            C("Moradia", D, "Essenciais", "mdi-home", "#5E35B1"),
            C("Mercado", D, "Essenciais", "mdi-cart", "#E53935"),
            C("Energia", D, "Essenciais", "mdi-flash", "#FDD835"),
            C("Água", D, "Essenciais", "mdi-water", "#039BE5"),
            C("Internet / Telefone", D, "Essenciais", "mdi-wifi", "#00ACC1"),
            C("Transporte / Combustível", D, "Pessoais", "mdi-car", "#6D4C41"),
            C("Saúde", D, "Pessoais", "mdi-medical-bag", "#D81B60"),
            C("Restaurantes / Delivery", D, "Pessoais", "mdi-silverware-fork-knife", "#F4511E"),
            C("Lazer", D, "Pessoais", "mdi-gamepad-variant", "#8E24AA"),
            C("Roupas", D, "Pessoais", "mdi-tshirt-crew", "#EC407A"),
            C("Assinaturas", D, "Pessoais", "mdi-play-box-multiple", "#7E57C2"),
            C("Compras diversas", D, "Pessoais", "mdi-shopping", "#FB8C00"),
            C("Educação", D, "Pessoais", "mdi-school", "#3949AB"),
            C("Tarifas e juros", D, "Financeiras", "mdi-bank", "#757575"),
            C("Empréstimos", D, "Financeiras", "mdi-hand-coin", "#546E7A"),
            C("Outros", D, "Outros", "mdi-dots-horizontal", "#9E9E9E"));

        db.DefinirAuditoria("Criação", "Cadastros iniciais");
        await db.SaveChangesAsync();
    }
}
