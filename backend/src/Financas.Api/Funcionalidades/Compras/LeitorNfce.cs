using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Compras;

/// <summary>
/// Lê a "consulta resumida" da NFC-e (a página que o QR code da nota abre). Só busca nos endereços oficiais
/// da Fazenda (lista abaixo): o sistema nunca acessa um endereço qualquer vindo de fora.
/// O layout (classes txtTit, RCod, Rqtd...) é o padrão nacional; por enquanto só SP está liberado.
/// </summary>
public partial class LeitorNfce(HttpClient http)
{
    private static readonly string[] HostsPermitidos = ["www.nfce.fazenda.sp.gov.br", "nfce.fazenda.sp.gov.br"];

    public async Task<NotaLida> Buscar(string? url, CancellationToken ct = default)
    {
        var endereco = Validar(url);
        string html;
        try
        {
            html = await http.GetStringAsync(endereco, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new RegraNegocioException("Não consegui abrir a nota no site da Fazenda. Confira a internet do PC e tente de novo.");
        }
        return Ler(html);
    }

    /// <summary>Aceita o link do QR code (com ou sem https://).</summary>
    public static Uri Validar(string? url)
    {
        var texto = url?.Trim() ?? "";
        if (texto.Length == 0) throw new RegraNegocioException("Cole o link da nota ou leia o QR code.");
        if (!texto.StartsWith("http", StringComparison.OrdinalIgnoreCase)) texto = "https://" + texto;
        if (!Uri.TryCreate(texto, UriKind.Absolute, out var uri) || !HostsPermitidos.Contains(uri.Host.ToLowerInvariant()))
            throw new RegraNegocioException("Esse link não é de uma nota fiscal de SP (NFC-e). Por enquanto só notas de SP.");
        return new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
    }

    public static NotaLida Ler(string html)
    {
        var chave = Digitos(Um(RegexChave(), html));
        if (chave.Length != 44)
            throw new RegraNegocioException("A página não trouxe uma nota válida (talvez ainda não esteja disponível; tente em alguns minutos).");

        var itens = new List<ItemNota>();
        foreach (Match m in RegexItem().Matches(html))
        {
            var bloco = m.Groups["bloco"].Value;
            itens.Add(new ItemNota(
                int.Parse(m.Groups["n"].Value),
                Texto(Um(RegexTitulo(), bloco)),
                NuloSeVazio(Digitos(Um(RegexCodigo(), bloco))),
                Numero(Um(RegexQtd(), bloco)),
                NuloSeVazio(Texto(Um(RegexUnidade(), bloco))),
                Numero(Um(RegexUnitario(), bloco)),
                Numero(Um(RegexValor(), bloco))));
        }
        if (itens.Count == 0) throw new RegraNegocioException("Não encontrei itens nesta nota.");

        // Linhas do rodapé: "rótulo → valor" (Valor total, Descontos, Valor a pagar e as formas de pagamento).
        var linhas = RegexLinha().Matches(html).Select(m => (Rotulo: Texto(m.Groups["rotulo"].Value), Valor: m.Groups["valor"].Value)).ToList();
        decimal? Linha(string inicio) =>
            linhas.Where(l => l.Rotulo.StartsWith(inicio, StringComparison.OrdinalIgnoreCase)).Select(l => (decimal?)Numero(l.Valor)).FirstOrDefault();

        var soma = itens.Sum(i => i.ValorTotal);
        var aPagar = Linha("Valor a pagar") ?? soma;
        var desconto = Linha("Descontos") ?? Math.Max(0, (Linha("Valor total") ?? soma) - aPagar);
        var pagamentos = RegexPagamento().Matches(html)
            .Select(m => new PagamentoNota(Texto(m.Groups["forma"].Value), Numero(m.Groups["valor"].Value)))
            .Where(p => !p.Forma.StartsWith("Troco", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var emissao = DateTime.TryParseExact(Um(RegexEmissao(), html), "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : throw new RegraNegocioException("Não encontrei a data de emissão da nota.");

        return new NotaLida(chave, Texto(Um(RegexLoja(), html)), NuloSeVazio(Texto(Um(RegexCnpj(), html))), emissao,
            Linha("Valor total") ?? soma, desconto, aPagar, itens, pagamentos);
    }

    private static string Um(Regex r, string texto) => r.Match(texto) is { Success: true } m ? m.Groups[1].Value : "";
    private static string Texto(string s) => Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(s, "<[^>]+>", " ")), @"\s+", " ").Trim();
    private static string Digitos(string s) => new(s.Where(char.IsDigit).ToArray());
    private static string? NuloSeVazio(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>Número no formato brasileiro ("1.234,56"). Vazio ou inválido = 0.</summary>
    private static decimal Numero(string s)
    {
        var limpo = Texto(s).Replace(".", "").Replace(",", ".");
        return decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    [GeneratedRegex(@"<span class=""chave"">([\d\s]+)</span>")] private static partial Regex RegexChave();
    [GeneratedRegex(@"<tr id=""Item \+ (?<n>\d+)"">(?<bloco>.*?)</tr>", RegexOptions.Singleline)] private static partial Regex RegexItem();
    [GeneratedRegex(@"<span class=""txtTit"">(.*?)</span>", RegexOptions.Singleline)] private static partial Regex RegexTitulo();
    [GeneratedRegex(@"<span class=""RCod"">(.*?)</span>", RegexOptions.Singleline)] private static partial Regex RegexCodigo();
    [GeneratedRegex(@"<span class=""Rqtd"">\s*<strong>[^<]*</strong>(.*?)</span>", RegexOptions.Singleline)] private static partial Regex RegexQtd();
    [GeneratedRegex(@"<span class=""RUN"">\s*<strong>[^<]*</strong>(.*?)</span>", RegexOptions.Singleline)] private static partial Regex RegexUnidade();
    [GeneratedRegex(@"<span class=""RvlUnit"">\s*<strong>[^<]*</strong>(.*?)</span>", RegexOptions.Singleline)] private static partial Regex RegexUnitario();
    [GeneratedRegex(@"<span class=""valor"">(.*?)</span>", RegexOptions.Singleline)] private static partial Regex RegexValor();
    [GeneratedRegex(@"<label>(?<rotulo>[^<]*)</label>\s*<span class=""totalNumb[^""]*"">(?<valor>[^<]*)</span>", RegexOptions.Singleline)] private static partial Regex RegexLinha();
    [GeneratedRegex(@"<label class=""tx"">(?<forma>[^<]*)</label>\s*<span class=""totalNumb"">(?<valor>[^<]*)</span>", RegexOptions.Singleline)] private static partial Regex RegexPagamento();
    [GeneratedRegex(@"Emiss[ãa]o:\s*</strong>\s*(\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2})")] private static partial Regex RegexEmissao();
    [GeneratedRegex(@"class=""txtTopo"">(.*?)</div>", RegexOptions.Singleline)] private static partial Regex RegexLoja();
    [GeneratedRegex(@"CNPJ:\s*([\d./-]+)")] private static partial Regex RegexCnpj();
}
