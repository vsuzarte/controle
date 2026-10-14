using Financas.Api.Infraestrutura;

// Usado pelo HEALTHCHECK do Docker (a imagem não tem curl).
if (args is ["healthcheck", ..])
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    try { return (await http.GetAsync("http://localhost:8080/health")).IsSuccessStatusCode ? 0 : 1; }
    catch { return 1; }
}

var builder = WebApplication.CreateBuilder(args);
builder.AdicionarFinancas();

var app = builder.Build();
await app.PrepararBanco();

app.UsarTratamentoDeErros();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapearFinancas();
// SPA: qualquer rota que não seja /api cai no index.html do Vue.
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;
