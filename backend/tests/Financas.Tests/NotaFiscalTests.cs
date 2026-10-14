using System.Net;
using Financas.Api.Funcionalidades.Compras;

namespace Financas.Tests;

/// <summary>Compras pela nota fiscal (NFC-e): leitura da página, importação por categoria e a busca "o que comprei".</summary>
public class NotaFiscalTests
{
    private const string Url = "https://www.nfce.fazenda.sp.gov.br/NFCeConsultaPublica/Paginas/ConsultaQRCode.aspx?p=35260000000000000000650010000000011000000019|2|1|1|abc";

    /// <summary>Página no mesmo formato da Fazenda de SP, com loja e itens inventados.</summary>
    private static string Pagina(string chave = "3526 0000 0000 0000 0000 6500 1000 0000 0110 0000 0019") => $$"""
        <div class="txtCenter">
          <div id="u20" class="txtTopo">MERCADO EXEMPLO LTDA</div>
          <div class="text">
                    CNPJ:
                    11.222.333/0001-44</div>
        </div>
        <table id="tabResult" class="filterable">
          <tr id="Item + 1">
            <td valign="top">
              <span class="txtTit">REFRIG COCA COLA 2L</span>
              <span class="RCod">
                    (Código:
                    7894900011517
                    )
                  </span>
              <br>
              <span class="Rqtd">
                <strong>Qtde.:</strong>2</span>
              <span class="RUN">
                <strong>UN: </strong>UN</span>
              <span class="RvlUnit">
                <strong>Vl. Unit.:</strong>
                    &nbsp;
                    10,5</span>
            </td>
            <td align="right" valign="top" class="txtTit noWrap">
                  Vl. Total
                  <br><span class="valor">21,00</span></td>
          </tr>
          <tr id="Item + 2">
            <td valign="top">
              <span class="txtTit">PAO FRANCES KG</span>
              <span class="RCod">(Código: 2769 )</span>
              <br>
              <span class="Rqtd"><strong>Qtde.:</strong>0,458</span>
              <span class="RUN"><strong>UN: </strong>KG</span>
              <span class="RvlUnit"><strong>Vl. Unit.:</strong> 16,99</span>
            </td>
            <td align="right" valign="top" class="txtTit noWrap">Vl. Total<br><span class="valor">7,78</span></td>
          </tr>
          <tr id="Item + 3">
            <td valign="top">
              <span class="txtTit">DETERGENTE YPE 500ML</span>
              <span class="RCod">(Código: 7896098900208 )</span>
              <br>
              <span class="Rqtd"><strong>Qtde.:</strong>1</span>
              <span class="RUN"><strong>UN: </strong>UN</span>
              <span class="RvlUnit"><strong>Vl. Unit.:</strong> 3,22</span>
            </td>
            <td align="right" valign="top" class="txtTit noWrap">Vl. Total<br><span class="valor">3,22</span></td>
          </tr>
        </table>
        <div id="totalNota" class="txtRight">
          <div id="linhaTotal"><label>Qtd. total de itens:</label><span class="totalNumb">3</span></div>
          <div id="linhaTotal"><label>Valor total R$:</label><span class="totalNumb">32,00</span></div>
          <div id="linhaTotal"><label>Descontos R$:</label><span class="totalNumb">2,00</span></div>
          <div id="linhaTotal" class="linhaShade"><label>Valor a pagar R$:</label><span class="totalNumb txtMax">30,00</span></div>
          <div id="linhaForma"><label>Forma de pagamento:</label><span class="totalNumb txtTitR">Valor pago R$:</span></div>
          <div id="linhaTotal"><label class="tx">
                    Cartão de Crédito
                  </label><span class="totalNumb">30,00</span></div>
          <div id="linhaTotal"><label class="tx">Troco </label><span class="totalNumb">NaN</span></div>
        </div>
        <li><strong>Número: </strong>11<strong> Série: </strong>1<strong> Emissão: </strong>12/10/2026 18:05:09 - Via Consumidor</li>
        <li><strong>Chave de acesso:</strong><br><span class="chave">{{chave}}</span></li>
        <h4>Consumidor</h4><li><strong>CPF: </strong>000.000.000-00</li>
        """;

    private sealed class PaginaFixa(string html) : HttpMessageHandler
    {
        public int Chamadas { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }
    }

    private static (CompraService Compras, ComprasConsultas Consultas) Servicos(Ambiente a, string? html = null) =>
        (new CompraService(a.Db, new LeitorNfce(new HttpClient(new PaginaFixa(html ?? Pagina()))), a.Lancamentos), new ComprasConsultas(a.Db));

    [Fact]
    public void Le_itens_quantidades_desconto_e_pagamento_sem_dados_do_consumidor()
    {
        var nota = LeitorNfce.Ler(Pagina());
        Assert.Equal("35260000000000000000650010000000011000000019", nota.Chave);
        Assert.Equal("MERCADO EXEMPLO LTDA", nota.Loja);
        Assert.Equal("11.222.333/0001-44", nota.Cnpj);
        Assert.Equal(new DateTime(2026, 10, 12, 18, 5, 9), nota.Emissao);
        Assert.Equal(3, nota.Itens.Count);
        Assert.Equal(new ItemNota(2, "PAO FRANCES KG", "2769", 0.458m, "KG", 16.99m, 7.78m), nota.Itens[1]);
        Assert.Equal("7894900011517", nota.Itens[0].Codigo);
        Assert.Equal((32m, 2m, 30m), (nota.ValorTotal, nota.Desconto, nota.ValorAPagar));
        Assert.Equal([new PagamentoNota("Cartão de Crédito", 30m)], nota.Pagamentos);
    }

    [Theory]
    [InlineData("https://www.google.com/?q=nfce.fazenda.sp.gov.br")]
    [InlineData("http://localhost:8080/api/contas")]
    [InlineData("https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx?p=1")]
    [InlineData("")]
    public void So_aceita_o_endereco_oficial_da_nota(string url) =>
        Assert.Throws<RegraNegocioException>(() => LeitorNfce.Validar(url));

    [Fact]
    public void Link_sem_https_vira_https() =>
        Assert.Equal("https", LeitorNfce.Validar("www.nfce.fazenda.sp.gov.br/NFCeConsultaPublica/Paginas/ConsultaQRCode.aspx?p=1").Scheme);

    [Theory]
    [InlineData(new[] { 21.0, 7.78, 3.22 }, 30.0)]
    [InlineData(new[] { 0.99, 0.99, 0.99 }, 2.0)]
    [InlineData(new[] { 10.0 }, 10.0)]
    public void Desconto_dividido_entre_os_itens_fecha_com_o_total(double[] itens, double pago)
    {
        var r = CompraService.Ratear(itens.Select(v => (decimal)v).ToList(), (decimal)pago);
        Assert.Equal((decimal)pago, r.Sum());
        Assert.All(r, v => Assert.True(v >= 0));
    }

    [Fact]
    public async Task Importar_cria_um_lancamento_por_categoria_e_nao_entra_duas_vezes()
    {
        using var a = new Ambiente();
        var bebidas = new Categoria { Nome = "Bebidas", Tipo = TipoCategoria.Despesa };
        a.Db.Categorias.Add(bebidas);
        await a.Db.SaveChangesAsync();
        var (compras, consultas) = Servicos(a);

        var lida = await compras.Ler(Url);
        Assert.Null(lida.CompraExistenteId);
        Assert.Equal(a.Cartao.Id, lida.ContaSugeridaId); // pago no crédito → cartão
        Assert.Equal(a.Mercado.Id, lida.CategoriaPadraoId);
        Assert.Equal(30m, lida.Itens.Sum(i => i.ValorPago));

        var compra = await compras.Importar(new ImportarCompraDto(Url, a.Cartao.Id, "Mercado do bairro",
            [new CategoriaDoItemDto(1, bebidas.Id), new CategoriaDoItemDto(2, a.Mercado.Id), new CategoriaDoItemDto(3, a.Mercado.Id)]));

        var lancs = await a.Db.Lancamentos.Where(l => l.CompraId == compra.Id).OrderBy(l => l.Valor).ToListAsync();
        Assert.Equal(2, lancs.Count);
        Assert.Equal(30m, lancs.Sum(l => l.Valor));
        Assert.All(lancs, l => Assert.Equal("Mercado do bairro", l.Descricao));
        Assert.All(lancs, l => Assert.Equal(D(2026, 11, 1), l.FaturaMes)); // compra no cartão: fatura certa
        Assert.Equal(bebidas.Id, lancs.Single(l => l.Valor > 15).CategoriaId);

        var view = await consultas.Obter(compra.Id);
        Assert.Equal(3, view.Itens.Count);
        Assert.Equal(0.458m, view.Itens[1].Quantidade); // milésimos guardados sem arredondar
        Assert.All(await a.Db.ItensCompra.ToListAsync(), i => Assert.NotNull(i.LancamentoId));

        await Assert.ThrowsAsync<RegraNegocioException>(() => compras.Importar(new ImportarCompraDto(Url, a.Cartao.Id, null, [])));
        Assert.Equal(compra.Id, (await compras.Ler(Url)).CompraExistenteId);
    }

    [Fact]
    public async Task Busca_o_que_comprei_soma_gasto_quantidade_e_preco()
    {
        using var a = new Ambiente();
        var (compras, consultas) = Servicos(a);
        await compras.Importar(new ImportarCompraDto(Url, a.Banco.Id, null, []));
        var outra = Servicos(a, Pagina("3526 0000 0000 0000 0000 6500 1000 0000 0220 0000 0028")).Compras;
        await outra.Importar(new ImportarCompraDto(Url, a.Banco.Id, null, []));

        var coca = (await consultas.Produtos("coca", D(2026, 1, 1), D(2026, 12, 31))).Single();
        Assert.Equal("REFRIG COCA COLA 2L", coca.Descricao);
        Assert.Equal(2, coca.Vezes);
        Assert.Equal(4m, coca.Quantidade);
        Assert.Equal(2 * CompraService.Ratear([21m, 7.78m, 3.22m], 30m)[0], coca.Total);
        Assert.Equal(3, (await consultas.Produtos(null, D(2026, 1, 1), D(2026, 12, 31))).Count);
    }

    [Fact]
    public async Task Categoria_escolhida_antes_vira_sugestao_para_o_mesmo_produto()
    {
        using var a = new Ambiente();
        var bebidas = new Categoria { Nome = "Bebidas", Tipo = TipoCategoria.Despesa };
        a.Db.Categorias.Add(bebidas);
        await a.Db.SaveChangesAsync();
        await Servicos(a).Compras.Importar(new ImportarCompraDto(Url, a.Banco.Id, null, [new CategoriaDoItemDto(1, bebidas.Id)]));

        var lida = await Servicos(a, Pagina("3526 0000 0000 0000 0000 6500 1000 0000 0330 0000 0037")).Compras.Ler(Url);
        Assert.Equal(bebidas.Id, lida.Itens[0].CategoriaId);   // mesma Coca (código de barras)
        Assert.Equal(a.Mercado.Id, lida.Itens[2].CategoriaId); // o resto continua Mercado
    }

    [Fact]
    public async Task Busca_tambem_acha_compras_lancadas_a_mao_mas_nao_contas_do_mes()
    {
        using var a = new Ambiente();
        var (compras, consultas) = Servicos(a);
        await compras.Importar(new ImportarCompraDto(Url, a.Banco.Id, null, []));
        await a.Lancamentos.Criar(new NovoLancamentoDto(TipoLancamento.Despesa, "Coca no bar", 8.5m, D(2026, 10, 10),
            a.Banco.Id, null, a.Lazer.Id, FormaPagamento.Pix, null, null, null, null));
        await a.Lancamentos.Criar(new NovoLancamentoDto(TipoLancamento.Despesa, "coca no bar ", 9.5m, D(2026, 10, 11),
            a.Banco.Id, null, a.Lazer.Id, FormaPagamento.Pix, null, null, null, null));
        await a.Recorrente(TipoLancamento.Despesa, a.Energia, 100, D(2026, 10, 1)); // conta do mês não é compra

        var achados = await consultas.Produtos("coca", D(2026, 1, 1), D(2026, 12, 31));
        Assert.Equal(2, achados.Count);
        var manual = achados.Single(p => p.Origem == ComprasConsultas.OrigemLancamento);
        Assert.Equal((2, 18m, "Banco"), (manual.Vezes, manual.Total, manual.UltimaLoja)); // mesmo nome, maiúsculas/espaços à parte
        Assert.Equal(ComprasConsultas.OrigemNota, achados.Single(p => p.Descricao.StartsWith("REFRIG")).Origem);
        Assert.DoesNotContain(await consultas.Produtos(null, D(2026, 1, 1), D(2026, 12, 31)), p => p.Descricao == "Energia");
    }
}
